using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Trace;

namespace Zaide.Features.Agents.Application.Transparency.Trace;

/// <summary>
/// Bounded capture queue with nonblocking admission and a single-consumer
/// drain task. The drain writes redacted payloads to the M1 durable Trace
/// record class. New submissions are rejected (not blocked) when the queue
/// is full, so the agent event pipeline is never delayed by trace capture.
/// </summary>
internal sealed class AgentTraceBoundedCaptureQueue : IDisposable
{
    private readonly BlockingCollection<AgentTraceBoundedCaptureItem> _queue;
    private readonly Task _drainTask;
    private readonly CancellationTokenSource _drainCts = new();
    private long _dropped;
    private long _admitted;
    private long _written;
    private bool _disposed;

    public AgentTraceBoundedCaptureQueue(
        AgentTraceCaptureLimits limits,
        IAgentDurableRecordStore store,
        AgentDurableWorkspaceStorageKeyResolver workspaceKeyResolver)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(workspaceKeyResolver);

        Limits = limits;
        Store = store;
        WorkspaceKeyResolver = workspaceKeyResolver;
        _queue = new BlockingCollection<AgentTraceBoundedCaptureItem>(limits.MaxQueueDepth);
        _drainTask = Task.Run(DrainAsync);
    }

    public AgentTraceCaptureLimits Limits { get; }

    public IAgentDurableRecordStore Store { get; }

    public AgentDurableWorkspaceStorageKeyResolver WorkspaceKeyResolver { get; }

    public long DroppedCount => Interlocked.Read(ref _dropped);

    public long AdmittedCount => Interlocked.Read(ref _admitted);

    public long WrittenCount => Interlocked.Read(ref _written);

    public bool TryEnqueue(AgentTraceBoundedCaptureItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (_disposed)
        {
            return false;
        }

        if (_queue.TryAdd(item) == false)
        {
            Interlocked.Increment(ref _dropped);
            return false;
        }

        Interlocked.Increment(ref _admitted);
        return true;
    }

    private async Task DrainAsync()
    {
        var token = _drainCts.Token;
        try
        {
            foreach (var item in _queue.GetConsumingEnumerable(token))
            {
                try
                {
                    PersistItem(item);
                    Interlocked.Increment(ref _written);
                }
                catch
                {
                    // Drain errors are counted as drops; never propagate to the
                    // event pipeline. M1 surfaces persist failures as
                    // ContentionFailed/WritesDisabled on the next attempt.
                    Interlocked.Increment(ref _dropped);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on Dispose.
        }
    }

    private void PersistItem(AgentTraceBoundedCaptureItem item)
    {
        var request = new AgentDurableRecordAppendRequest(
            item.WorkspaceKey,
            AgentDurableRecordClass.Trace,
            idempotencyKey: item.IdempotencyKey,
            payloadJson: item.RedactedPayloadJson,
            scopeReferences: new AgentDurableRecordScopeReferences(
                conversationId: item.Scope.ConversationId,
                sessionId: item.Scope.SessionId,
                runId: item.Scope.RunId,
                backendId: item.BackendId),
            recordedAtUtc: item.RecordedAtUtc);

        Store.TryAppend(request);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _queue.CompleteAdding();
        }
        catch
        {
            // Best-effort close.
        }

        try
        {
            _drainCts.Cancel();
        }
        catch
        {
            // Best-effort cancel.
        }

        try
        {
            _drainTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Drain may already be exiting; do not block disposal.
        }

        _drainCts.Dispose();
        _queue.Dispose();
    }
}

/// <summary>
/// One redacted trace item waiting in the bounded capture queue.
/// </summary>
internal sealed class AgentTraceBoundedCaptureItem
{
    public AgentTraceBoundedCaptureItem(
        AgentDurableWorkspaceStorageKey workspaceKey,
        string backendId,
        string idempotencyKey,
        string redactedPayloadJson,
        AgentTraceRecordScope scope,
        DateTimeOffset recordedAtUtc)
    {
        WorkspaceKey = workspaceKey;
        BackendId = backendId;
        IdempotencyKey = idempotencyKey;
        RedactedPayloadJson = redactedPayloadJson;
        Scope = scope;
        RecordedAtUtc = recordedAtUtc;
    }

    public AgentDurableWorkspaceStorageKey WorkspaceKey { get; }

    public string BackendId { get; }

    public string IdempotencyKey { get; }

    public string RedactedPayloadJson { get; }

    public AgentTraceRecordScope Scope { get; }

    public DateTimeOffset RecordedAtUtc { get; }
}

/// <summary>
/// Resolves a workspace key for a trace capture request. The M2 production
/// default derives the key from the active workspace root path, matching the
/// M1 record partition convention.
/// </summary>
internal abstract class AgentDurableWorkspaceStorageKeyResolver
{
    public abstract AgentDurableWorkspaceStorageKey Resolve(string? workspaceRoot);
}

/// <summary>
/// Resolver that derives the workspace storage key from a normalized
/// workspace root path. Used by the capture sink so a missing or null root
/// still yields a stable "unbound" partition key for honest unavailability.
/// </summary>
internal sealed class PathDerivedAgentDurableWorkspaceStorageKeyResolver
    : AgentDurableWorkspaceStorageKeyResolver
{
    public const string UnboundWorkspaceKey = "ws:unbound";

    public override AgentDurableWorkspaceStorageKey Resolve(string? workspaceRoot)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            return AgentDurableWorkspaceStorageKey.FromValue(UnboundWorkspaceKey);
        }

        return AgentDurableWorkspaceStorageKey.FromWorkspaceRoot(workspaceRoot);
    }
}
