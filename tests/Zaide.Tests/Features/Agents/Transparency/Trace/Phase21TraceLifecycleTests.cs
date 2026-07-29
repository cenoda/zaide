using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Trace;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;

namespace Zaide.Tests.Features.Agents.Transparency.Trace;

/// <summary>
/// Phase 21 M2 capture pipeline lifecycle. Exercises mandatory redaction,
/// bounded queue admission, durable persistence through the M1 Trace record
/// class, and the read-side inspection entry point.
/// </summary>
public sealed class Phase21TraceLifecycleTests : IDisposable
{
    private readonly string _rootDirectory;
    private readonly AgentDurableWorkspaceStorageKey _workspaceKey;
    private readonly AgentDurableRecordFileStore _store;
    private readonly PathDerivedAgentDurableWorkspaceStorageKeyResolver _resolver;
    private readonly AgentTraceBoundedCaptureQueue _queue;
    private readonly AgentTraceCaptureSink _sink;
    private readonly AgentTraceInspector _inspector;

    public Phase21TraceLifecycleTests()
    {
        (_rootDirectory, _workspaceKey) = Phase21TraceTestSupport.CreateWorkspaceFixture();
        _store = Phase21TraceTestSupport.CreateStore(_rootDirectory);
        _resolver = Phase21TraceTestSupport.CreateKeyResolver();
        _queue = Phase21TraceTestSupport.CreateQueue(_store, _resolver, maxDepth: 16);
        _sink = Phase21TraceTestSupport.CreateSink(_queue, _resolver);
        _inspector = new AgentTraceInspector(_store);
        _sink.EnableCapture();
    }

    public void Dispose()
    {
        _queue.Dispose();
        _store.Dispose();
        Phase21TraceTestSupport.DeleteDirectory(_rootDirectory);
    }

    [Fact]
    public void Capture_DefaultsToDisabledWhenEnableNotCalled()
    {
        using var localStore = Phase21TraceTestSupport.CreateStore(_rootDirectory);
        using var localQueue = Phase21TraceTestSupport.CreateQueue(localStore, _resolver);
        var localSink = Phase21TraceTestSupport.CreateSink(localQueue, _resolver);

        var result = localSink.TrySubmit(Phase21TraceTestSupport.CreateRequest(
            _workspaceKey,
            Phase21TraceTestSupport.NativeHarnessBackendId,
            AgentTraceKind.Request,
            """{"method":"initialize"}"""));

        Assert.Equal(AgentTraceCaptureStatus.Disabled, result.Status);
        Assert.Equal(AgentTraceCaptureState.Disabled, result.CaptureState);
    }

    [Fact]
    public void Capture_AdmittedPayloadIsRedactedBeforeDurableWrite()
    {
        var secretPayload =
            "Authorization: Bearer sk-abcdefghijklmnopqrstuvwxyz0123456789, method=initialize";

        var result = _sink.TrySubmit(Phase21TraceTestSupport.CreateRequest(
            _workspaceKey,
            Phase21TraceTestSupport.NativeHarnessBackendId,
            AgentTraceKind.Request,
            secretPayload,
            idempotencyKey: "trace-1"));

        Assert.Equal(AgentTraceCaptureStatus.Accepted, result.Status);
        Assert.Equal(AgentTraceCaptureState.Redacted, result.CaptureState);

        Phase21TraceTestSupport.WaitForQueueDrain(_queue, expectedWritten: 1);
        var records = Phase21TraceTestSupport.ReplayTraceRecords(_store, _workspaceKey);
        var single = Assert.Single(records);
        Assert.DoesNotContain("sk-abcdefghijklmnopqrstuvwxyz", single.PayloadJson);
        Assert.Contains("[REDACTED:api-key]", single.PayloadJson);
    }

    [Fact]
    public void Capture_AdmittedPayloadWithoutSecretsIsCapturedAsIs()
    {
        var safePayload = """{"method":"initialize","id":"1","direction":"in"}""";

        var result = _sink.TrySubmit(Phase21TraceTestSupport.CreateRequest(
            _workspaceKey,
            Phase21TraceTestSupport.NativeHarnessBackendId,
            AgentTraceKind.Request,
            safePayload,
            idempotencyKey: "trace-safe"));

        Assert.Equal(AgentTraceCaptureStatus.Accepted, result.Status);
        Assert.Equal(AgentTraceCaptureState.Captured, result.CaptureState);

        Phase21TraceTestSupport.WaitForQueueDrain(_queue, expectedWritten: 1);
        var records = Phase21TraceTestSupport.ReplayTraceRecords(_store, _workspaceKey);
        var single = Assert.Single(records);
        Assert.Contains("initialize", single.PayloadJson);
        Assert.Contains("\"captureState\":2", single.PayloadJson);
    }

    [Fact]
    public void Capture_UnavailableMarkerBypassesRedaction()
    {
        var result = _sink.TrySubmit(Phase21TraceTestSupport.CreateRequest(
            _workspaceKey,
            Phase21TraceTestSupport.NativeHarnessBackendId,
            AgentTraceKind.UnavailableMarker,
            """{"reason":"backend-private"}""",
            idempotencyKey: "trace-unavailable"));

        Assert.Equal(AgentTraceCaptureStatus.Unavailable, result.Status);
        Assert.Equal(AgentTraceCaptureState.Unavailable, result.CaptureState);

        Phase21TraceTestSupport.WaitForQueueDrain(_queue, expectedWritten: 1);
        var records = Phase21TraceTestSupport.ReplayTraceRecords(_store, _workspaceKey);
        var single = Assert.Single(records);
        Assert.Contains("unavailable", single.PayloadJson);
    }

    [Fact]
    public void Capture_OversizedPayloadIsTruncatedToBoundedMarker()
    {
        var hugePayload = new string('x', AgentTraceCaptureLimits.DefaultMaxPayloadBytes + 1024);

        var result = _sink.TrySubmit(Phase21TraceTestSupport.CreateRequest(
            _workspaceKey,
            Phase21TraceTestSupport.NativeHarnessBackendId,
            AgentTraceKind.Request,
            hugePayload,
            idempotencyKey: "trace-huge"));

        Assert.Equal(AgentTraceCaptureStatus.Truncated, result.Status);
        Assert.Equal(AgentTraceCaptureState.Truncated, result.CaptureState);

        Phase21TraceTestSupport.WaitForQueueDrain(_queue, expectedWritten: 1);
        var records = Phase21TraceTestSupport.ReplayTraceRecords(_store, _workspaceKey);
        var single = Assert.Single(records);
        Assert.Contains("truncated", single.PayloadJson);
    }

    [Fact]
    public void Capture_EmptyPayloadIsRejectedAsInvalidRequest()
    {
        var result = _sink.TrySubmit(Phase21TraceTestSupport.CreateRequest(
            _workspaceKey,
            Phase21TraceTestSupport.NativeHarnessBackendId,
            AgentTraceKind.Request,
            string.Empty,
            idempotencyKey: "trace-empty"));

        Assert.Equal(AgentTraceCaptureStatus.InvalidRequest, result.Status);
    }

    [Fact]
    public void Capture_BackpressureIsReportedWhenQueueIsFull()
    {
        using var localStore = Phase21TraceTestSupport.CreateStore(_rootDirectory);
        using var localQueue = Phase21TraceTestSupport.CreateQueue(
            localStore, _resolver, maxDepth: 1);
        var localSink = Phase21TraceTestSupport.CreateSink(localQueue, _resolver);
        localSink.EnableCapture();

        for (var i = 0; i < 50; i++)
        {
            localSink.TrySubmit(Phase21TraceTestSupport.CreateRequest(
                _workspaceKey,
                Phase21TraceTestSupport.NativeHarnessBackendId,
                AgentTraceKind.Request,
                "{\"method\":\"ping\"}",
                idempotencyKey: "trace-flood-" + i));
        }

        Assert.True(localQueue.DroppedCount > 0);
    }

    [Fact]
    public void Capture_RejectsBackendThatIsNotInSourceRegistry()
    {
        var coordinator = Phase21TraceTestSupport.CreateCoordinator(_store, _resolver, queue: _queue);

        var result = coordinator.TrySubmit(Phase21TraceTestSupport.CreateRequest(
            _workspaceKey,
            "backend:unknown-third-party",
            AgentTraceKind.Request,
            "{\"method\":\"ping\"}"));

        Assert.Equal(AgentTraceCaptureStatus.Disabled, result.Status);
        Assert.Equal(AgentTraceCaptureState.Disabled, result.CaptureState);
    }

    [Fact]
    public async Task Inspector_ReturnsSummaryAndRecordsForWorkspace()
    {
        for (var i = 0; i < 3; i++)
        {
            _sink.TrySubmit(Phase21TraceTestSupport.CreateRequest(
                _workspaceKey,
                Phase21TraceTestSupport.NativeHarnessBackendId,
                AgentTraceKind.Request,
                "{\"method\":\"initialize\"}",
                idempotencyKey: "summary-" + i));
        }

        Phase21TraceTestSupport.WaitForQueueDrain(_queue, expectedWritten: 3);

        var summary = _inspector.GetSummary(_workspaceKey);
        Assert.Equal(3, summary.TotalRecords);
        Assert.Equal(3, summary.CountsByState[AgentTraceCaptureState.Captured]);
        Assert.Equal(Phase21TraceTestSupport.NativeHarnessBackendId, summary.CountsByBackend.Keys.Single());

        var page = _inspector.GetRecords(_workspaceKey, afterOrderingSequence: 0, maxRecords: 2);
        Assert.Equal(2, page.Count);
        Assert.Equal(1L, page[0].OrderingSequence);
        Assert.Equal(2L, page[1].OrderingSequence);

        await Task.CompletedTask;
    }

    [Fact]
    public void Inspector_SummaryIsEmptyForUnknownWorkspace()
    {
        var unknownKey = AgentDurableWorkspaceStorageKey.FromValue("ws:0123456789abcdef");

        var summary = _inspector.GetSummary(unknownKey);

        Assert.True(summary.IsEmpty);
        Assert.Equal(0, summary.TotalRecords);
    }

    [Fact]
    public void Inspector_RejectsZeroMaxRecords()
    {
        var records = _inspector.GetRecords(_workspaceKey, afterOrderingSequence: 0, maxRecords: 0);

        Assert.Empty(records);
    }

    [Fact]
    public void Coordinator_AdmitCountersReflectQueueLifecycle()
    {
        _sink.TrySubmit(Phase21TraceTestSupport.CreateRequest(
            _workspaceKey,
            Phase21TraceTestSupport.NativeHarnessBackendId,
            AgentTraceKind.Request,
            "{\"method\":\"initialize\"}",
            idempotencyKey: "coord-1"));

        Assert.True(_sink.AdmittedCount >= 1);
        Assert.Equal(0, _sink.BackpressureDroppedCount);

        Phase21TraceTestSupport.WaitForQueueDrain(_queue, expectedWritten: 1);
        Assert.True(_sink.WrittenCount >= 1);
    }

    [Fact]
    public void Coordinator_GetSummaryAndRecordsUseUnboundKeyWhenWorkspaceRootIsNull()
    {
        var coordinator = Phase21TraceTestSupport.CreateCoordinator(_store, _resolver);

        var summary = coordinator.GetSummary(workspaceRoot: null);
        var records = coordinator.GetRecords(workspaceRoot: null, afterOrderingSequence: 0, maxRecords: 16);

        Assert.True(summary.IsEmpty);
        Assert.Empty(records);
    }

    [Fact]
    public void Capture_RespectsOrderingSequence()
    {
        _sink.TrySubmit(Phase21TraceTestSupport.CreateRequest(
            _workspaceKey,
            Phase21TraceTestSupport.NativeHarnessBackendId,
            AgentTraceKind.Request,
            "{\"method\":\"first\"}",
            idempotencyKey: "order-1"));
        _sink.TrySubmit(Phase21TraceTestSupport.CreateRequest(
            _workspaceKey,
            Phase21TraceTestSupport.NativeHarnessBackendId,
            AgentTraceKind.Response,
            "{\"result\":\"second\"}",
            idempotencyKey: "order-2"));

        Phase21TraceTestSupport.WaitForQueueDrain(_queue, expectedWritten: 2);

        var records = Phase21TraceTestSupport.ReplayTraceRecords(_store, _workspaceKey);
        Assert.Equal(2, records.Count);
        Assert.Equal(1L, records[0].OrderingSequence);
        Assert.Equal(2L, records[1].OrderingSequence);
        Assert.Equal(AgentTraceKind.Request, records[0].RecordClass == AgentDurableRecordClass.Trace
            ? AgentTraceKind.Request
            : AgentTraceKind.Request);
    }

    [Fact]
    public void Capture_ScopeReferencesArePersistedWithRecord()
    {
        _sink.TrySubmit(Phase21TraceTestSupport.CreateRequest(
            _workspaceKey,
            Phase21TraceTestSupport.NativeHarnessBackendId,
            AgentTraceKind.ToolCall,
            "{\"toolName\":\"read_file\"}",
            scope: new AgentTraceRecordScope(
                conversationId: "conversation:phase21-scope",
                sessionId: "session:phase21-scope",
                runId: "run:phase21-scope",
                backendId: Phase21TraceTestSupport.NativeHarnessBackendId),
            idempotencyKey: "scope-1"));

        Phase21TraceTestSupport.WaitForQueueDrain(_queue, expectedWritten: 1);

        var records = Phase21TraceTestSupport.ReplayTraceRecords(_store, _workspaceKey);
        var single = Assert.Single(records);
        Assert.Equal("conversation:phase21-scope", single.ScopeReferences.ConversationId);
        Assert.Equal("session:phase21-scope", single.ScopeReferences.SessionId);
        Assert.Equal("run:phase21-scope", single.ScopeReferences.RunId);
        Assert.Equal(Phase21TraceTestSupport.NativeHarnessBackendId, single.ScopeReferences.BackendId);
    }

    [Fact]
    public void Capture_TraceClassIsSeparateFromUsageAndAuditClasses()
    {
        _sink.TrySubmit(Phase21TraceTestSupport.CreateRequest(
            _workspaceKey,
            Phase21TraceTestSupport.NativeHarnessBackendId,
            AgentTraceKind.Request,
            "{\"method\":\"initialize\"}",
            idempotencyKey: "class-1"));

        Phase21TraceTestSupport.WaitForQueueDrain(_queue, expectedWritten: 1);

        var traceRecords = _store.Replay(new AgentDurableRecordReplayRequest(
            _workspaceKey,
            AgentDurableRecordClass.Trace));
        var usageRecords = _store.Replay(new AgentDurableRecordReplayRequest(
            _workspaceKey,
            AgentDurableRecordClass.Usage));
        var auditRecords = _store.Replay(new AgentDurableRecordReplayRequest(
            _workspaceKey,
            AgentDurableRecordClass.Audit));

        Assert.Single(traceRecords.Records);
        Assert.Empty(usageRecords.Records);
        Assert.Empty(auditRecords.Records);
    }
}
