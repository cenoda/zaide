using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Contracts.Transparency.Trace;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Trace;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;

namespace Zaide.Tests.Features.Agents.Transparency.Trace;

/// <summary>
/// Shared M2 test fixtures. Each test creates an isolated temp directory,
/// derives a workspace storage key, and wires the capture pipeline directly
/// against an <see cref="IAgentDurableRecordStore"/>. No service-locator
/// usage; tests stay independent of the production composition root.
/// </summary>
internal static class Phase21TraceTestSupport
{
    public const string NativeHarnessBackendId = "backend:zaide-native-harness";
    public const string AcpBackendId = "backend:acp";

    public static (string RootDirectory, AgentDurableWorkspaceStorageKey WorkspaceKey) CreateWorkspaceFixture()
    {
        var rootDirectory = Path.Combine(
            Path.GetTempPath(),
            "ZaidePhase21Trace_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootDirectory);
        var workspaceRoot = Path.Combine(rootDirectory, "workspace");
        Directory.CreateDirectory(workspaceRoot);
        var workspaceKey = AgentDurableWorkspaceStorageKey.FromWorkspaceRoot(workspaceRoot);
        return (rootDirectory, workspaceKey);
    }

    public static AgentDurableRecordFileStore CreateStore(string rootDirectory) =>
        new(
            rootDirectory,
            new AgentDurableRecordMigrator(new IAgentDurableRecordMigration[]
            {
                new AgentDurableRecordMigrationV0ToV1(),
            }));

    public static PathDerivedAgentDurableWorkspaceStorageKeyResolver CreateKeyResolver() =>
        new();

    public static AgentTraceBoundedCaptureQueue CreateQueue(
        AgentDurableRecordFileStore store,
        AgentDurableWorkspaceStorageKeyResolver resolver,
        int maxDepth = 64) =>
        new(new AgentTraceCaptureLimits(maxQueueDepth: maxDepth), store, resolver);

    public static AgentTraceCaptureSink CreateSink(
        AgentTraceBoundedCaptureQueue queue,
        AgentDurableWorkspaceStorageKeyResolver resolver) =>
        new(AgentTraceCaptureLimits.Default, queue, resolver);

    public static AgentTraceCoordinator CreateCoordinator(
        AgentDurableRecordFileStore store,
        AgentDurableWorkspaceStorageKeyResolver resolver,
        AgentTraceCaptureSink? sink = null,
        IAgentTraceSourceRegistry? registry = null,
        AgentTraceBoundedCaptureQueue? queue = null)
    {
        if (queue is null)
        {
            queue = CreateQueue(store, resolver);
        }

        sink ??= CreateSink(queue, resolver);
        registry ??= new AgentTraceSourceRegistry();
        var inspector = new AgentTraceInspector(store);
        return new AgentTraceCoordinator(sink, inspector, registry, resolver);
    }

    public static void WaitForQueueDrain(
        AgentTraceBoundedCaptureQueue queue,
        long expectedWritten,
        TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(2));
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (queue.WrittenCount >= expectedWritten && queue.AdmittedCount >= expectedWritten)
            {
                return;
            }

            Thread.Sleep(10);
        }
    }

    public static AgentTraceCaptureRequest CreateRequest(
        AgentDurableWorkspaceStorageKey workspaceKey,
        string backendId,
        AgentTraceKind kind,
        string payloadJson,
        AgentTraceRecordScope? scope = null,
        string? idempotencyKey = null) =>
        new(
            workspaceKey,
            backendId,
            kind,
            AgentTraceEvidenceLevel.BackendExecutedAndReported,
            payloadJson,
            scope ?? new AgentTraceRecordScope(
                conversationId: "conversation:trace-test",
                sessionId: "session:trace-test",
                runId: "run:trace-test",
                backendId: backendId),
            idempotencyKey: idempotencyKey);

    public static IReadOnlyList<AgentDurableRecordEnvelope> ReplayTraceRecords(
        AgentDurableRecordFileStore store,
        AgentDurableWorkspaceStorageKey workspaceKey,
        long afterOrderingSequence = 0,
        int maxRecords = 256)
    {
        var replay = store.Replay(new AgentDurableRecordReplayRequest(
            workspaceKey,
            AgentDurableRecordClass.Trace,
            afterOrderingSequence,
            maxRecords));
        return replay.Records;
    }

    public static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}
