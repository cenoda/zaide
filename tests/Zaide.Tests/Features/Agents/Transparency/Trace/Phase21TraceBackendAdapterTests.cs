using System;
using System.Linq;
using System.Text.Json;
using Xunit;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Contracts.Transparency.Trace;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Trace;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;

namespace Zaide.Tests.Features.Agents.Transparency.Trace;

/// <summary>
/// Phase 21 M2 backend evidence adapter behavior. The Native Harness and
/// ACP sources produce neutral trace inputs without sharing
/// backend-private internals. Each adapter must register, redact, and
/// persist through the M1 Trace record class.
/// </summary>
public sealed class Phase21TraceBackendAdapterTests : IDisposable
{
    private readonly string _rootDirectory;
    private readonly AgentDurableWorkspaceStorageKey _workspaceKey;
    private readonly AgentDurableRecordFileStore _store;
    private readonly PathDerivedAgentDurableWorkspaceStorageKeyResolver _resolver;
    private readonly AgentTraceBoundedCaptureQueue _queue;
    private readonly AgentTraceCaptureSink _sink;
    private readonly AgentTraceSourceRegistry _registry;
    private readonly AgentTraceCoordinator _coordinator;
    private readonly AgentTraceBackendEvidenceSourceWriter _writer;
    private readonly NativeHarnessAgentTraceSource _nativeHarnessSource;
    private readonly AcpAgentTraceSource _acpSource;

    public Phase21TraceBackendAdapterTests()
    {
        (_rootDirectory, _workspaceKey) = Phase21TraceTestSupport.CreateWorkspaceFixture();
        _store = Phase21TraceTestSupport.CreateStore(_rootDirectory);
        _resolver = Phase21TraceTestSupport.CreateKeyResolver();
        _queue = Phase21TraceTestSupport.CreateQueue(_store, _resolver, maxDepth: 16);
        _sink = Phase21TraceTestSupport.CreateSink(_queue, _resolver);
        _registry = new AgentTraceSourceRegistry();
        _coordinator = new AgentTraceCoordinator(_sink, new AgentTraceInspector(_store), _registry, _resolver);
        _writer = new AgentTraceBackendEvidenceSourceWriter(_coordinator);
        _nativeHarnessSource = new NativeHarnessAgentTraceSource(_writer);
        _acpSource = new AcpAgentTraceSource(_writer);
        _registry.Register(_nativeHarnessSource);
        _registry.Register(_acpSource);
        _sink.EnableCapture();
    }

    public void Dispose()
    {
        _queue.Dispose();
        _store.Dispose();
        Phase21TraceTestSupport.DeleteDirectory(_rootDirectory);
    }

    [Fact]
    public void NativeHarnessSource_ExposesExpectedBackendId()
    {
        Assert.Equal(AgentBackendIds.NativeHarnessValue, _nativeHarnessSource.BackendId);
    }

    [Fact]
    public void AcpSource_ExposesExpectedBackendId()
    {
        Assert.Equal(AgentBackendIds.AcpValue, _acpSource.BackendId);
    }

    [Fact]
    public void NativeHarnessSource_CanExposeRequestResponseAndLoopHistoryKinds()
    {
        Assert.True(_nativeHarnessSource.CanExpose(AgentTraceKind.Request));
        Assert.True(_nativeHarnessSource.CanExpose(AgentTraceKind.Response));
        Assert.True(_nativeHarnessSource.CanExpose(AgentTraceKind.ToolCall));
        Assert.True(_nativeHarnessSource.CanExpose(AgentTraceKind.ToolResult));
        Assert.True(_nativeHarnessSource.CanExpose(AgentTraceKind.BackendLoopHistory));
    }

    [Fact]
    public void AcpSource_CanExposeProtocolFrameKind()
    {
        Assert.True(_acpSource.CanExpose(AgentTraceKind.ProtocolFrame));
        Assert.True(_acpSource.CanExpose(AgentTraceKind.Request));
        Assert.True(_acpSource.CanExpose(AgentTraceKind.Response));
        Assert.True(_acpSource.CanExpose(AgentTraceKind.Error));
    }

    [Fact]
    public void NativeHarnessSource_RejectsProtocolFrameKind()
    {
        Assert.False(_nativeHarnessSource.CanExpose(AgentTraceKind.ProtocolFrame));
    }

    [Fact]
    public void AcpSource_RejectsToolCallAndLoopHistoryKinds()
    {
        Assert.False(_acpSource.CanExpose(AgentTraceKind.ToolCall));
        Assert.False(_acpSource.CanExpose(AgentTraceKind.BackendLoopHistory));
    }

    [Fact]
    public void NativeHarnessSource_LoopHistoryTurnJsonHasNeutralShape()
    {
        var json = NativeHarnessAgentTraceSource.SerializeLoopHistoryTurn(
            backendId: AgentBackendIds.NativeHarnessValue,
            kindLabel: "assistant",
            turnIndex: 3,
            recordedAtUtc: new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero),
            publicText: "I'll read the file now.");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(AgentBackendIds.NativeHarnessValue, root.GetProperty("backend").GetString());
        Assert.Equal("assistant", root.GetProperty("kind").GetString());
        Assert.Equal(3, root.GetProperty("turnIndex").GetInt32());
        Assert.Equal("I'll read the file now.", root.GetProperty("publicText").GetString());
    }

    [Fact]
    public void AcpSource_ProtocolFrameJsonUsesOpaqueBodyMarker()
    {
        var body = "{\"jsonrpc\":\"2.0\",\"method\":\"initialize\"}";
        var marker = AgentTraceBackendEvidenceSourceWriter.ComputeOpaqueBodyMarker(body);

        var json = AcpAgentTraceSource.SerializeProtocolFrame(
            backendId: AgentBackendIds.AcpValue,
            method: "initialize",
            id: "1",
            direction: "in",
            observedAtUtc: new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero),
            opaqueBodyBase64: marker);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(AgentBackendIds.AcpValue, root.GetProperty("backend").GetString());
        Assert.Equal("initialize", root.GetProperty("method").GetString());
        Assert.Equal("in", root.GetProperty("direction").GetString());
        Assert.Equal(marker, root.GetProperty("opaqueBodyBase64").GetString());
    }

    [Fact]
    public void NativeHarnessSource_SubmitsRedactedLoopHistoryTurn()
    {
        var secretPayload = NativeHarnessAgentTraceSource.SerializeLoopHistoryTurn(
            backendId: AgentBackendIds.NativeHarnessValue,
            kindLabel: "user",
            turnIndex: 1,
            recordedAtUtc: DateTimeOffset.UtcNow,
            publicText: "Authorization: Bearer sk-abcdefghijklmnopqrstuvwxyz0123456789");

        var request = Phase21TraceTestSupport.CreateRequest(
            _workspaceKey,
            AgentBackendIds.NativeHarnessValue,
            AgentTraceKind.BackendLoopHistory,
            secretPayload,
            idempotencyKey: "nh-1");

        var result = _nativeHarnessSource.Submit(request);

        Assert.Equal(AgentTraceCaptureStatus.Accepted, result.Status);
        Assert.Equal(AgentTraceCaptureState.Redacted, result.CaptureState);
        Phase21TraceTestSupport.WaitForQueueDrain(_queue, expectedWritten: 1);
        var records = Phase21TraceTestSupport.ReplayTraceRecords(_store, _workspaceKey);
        var single = Assert.Single(records);
        Assert.DoesNotContain("sk-abcdefghijklmnopqrstuvwxyz", single.PayloadJson);
        Assert.Contains("[REDACTED:api-key]", single.PayloadJson);
    }

    [Fact]
    public void AcpSource_SubmitsProtocolFrameWithoutLeakingSecretBody()
    {
        // The ACP body is hashed into opaqueBodyBase64 before submission; the
        // redaction processor scans the visible JSON, not the original body.
        // The capture state is Captured (not Redacted) because the secret
        // is hidden behind the SHA-256 marker; the source does not share
        // backend-private internals.
        var body = "{\"jsonrpc\":\"2.0\",\"method\":\"session/new\",\"params\":{\"apiKey\":\"sk-abcdefghijklmnopqrstuvwxyz0123456789\"}}";
        var marker = AgentTraceBackendEvidenceSourceWriter.ComputeOpaqueBodyMarker(body);
        var payload = AcpAgentTraceSource.SerializeProtocolFrame(
            backendId: AgentBackendIds.AcpValue,
            method: "session/new",
            id: "2",
            direction: "out",
            observedAtUtc: DateTimeOffset.UtcNow,
            opaqueBodyBase64: marker);

        var request = Phase21TraceTestSupport.CreateRequest(
            _workspaceKey,
            AgentBackendIds.AcpValue,
            AgentTraceKind.ProtocolFrame,
            payload,
            idempotencyKey: "acp-1");

        var result = _acpSource.Submit(request);

        Assert.Equal(AgentTraceCaptureStatus.Accepted, result.Status);
        Assert.Equal(AgentTraceCaptureState.Captured, result.CaptureState);
        Phase21TraceTestSupport.WaitForQueueDrain(_queue, expectedWritten: 1);
        var records = Phase21TraceTestSupport.ReplayTraceRecords(_store, _workspaceKey);
        var single = Assert.Single(records);
        Assert.DoesNotContain("sk-abcdefghijklmnopqrstuvwxyz", single.PayloadJson);
        Assert.Contains("opaqueBodyBase64", single.PayloadJson);
    }

    [Fact]
    public void AcpSource_RedactsSensitiveMethodName()
    {
        // Sensitive token in the public method metadata is still redacted.
        var body = "{\"jsonrpc\":\"2.0\",\"method\":\"initialize\"}";
        var marker = AgentTraceBackendEvidenceSourceWriter.ComputeOpaqueBodyMarker(body);
        var payload = AcpAgentTraceSource.SerializeProtocolFrame(
            backendId: AgentBackendIds.AcpValue,
            method: "auth-Authorization Bearer sk-abcdefghijklmnopqrstuvwxyz0123456789",
            id: "3",
            direction: "in",
            observedAtUtc: DateTimeOffset.UtcNow,
            opaqueBodyBase64: marker);

        var request = Phase21TraceTestSupport.CreateRequest(
            _workspaceKey,
            AgentBackendIds.AcpValue,
            AgentTraceKind.ProtocolFrame,
            payload,
            idempotencyKey: "acp-sensitive-method");

        var result = _acpSource.Submit(request);

        Assert.Equal(AgentTraceCaptureStatus.Accepted, result.Status);
        Assert.Equal(AgentTraceCaptureState.Redacted, result.CaptureState);
        Phase21TraceTestSupport.WaitForQueueDrain(_queue, expectedWritten: 1);
        var records = Phase21TraceTestSupport.ReplayTraceRecords(_store, _workspaceKey);
        var single = Assert.Single(records);
        Assert.DoesNotContain("sk-abcdefghijklmnopqrstuvwxyz", single.PayloadJson);
        Assert.Contains("[REDACTED:api-key]", single.PayloadJson);
    }

    [Fact]
    public void NativeHarnessSource_RejectsUnsupportedKind()
    {
        var request = Phase21TraceTestSupport.CreateRequest(
            _workspaceKey,
            AgentBackendIds.NativeHarnessValue,
            AgentTraceKind.ProtocolFrame,
            "{\"method\":\"x\"}",
            idempotencyKey: "nh-unsupported");

        var result = _nativeHarnessSource.Submit(request);

        Assert.Equal(AgentTraceCaptureStatus.Disabled, result.Status);
        Assert.Equal(AgentTraceCaptureState.Unavailable, result.CaptureState);
    }

    [Fact]
    public void AcpSource_RejectsUnsupportedKind()
    {
        var request = Phase21TraceTestSupport.CreateRequest(
            _workspaceKey,
            AgentBackendIds.AcpValue,
            AgentTraceKind.ToolCall,
            "{\"tool\":\"x\"}",
            idempotencyKey: "acp-unsupported");

        var result = _acpSource.Submit(request);

        Assert.Equal(AgentTraceCaptureStatus.Disabled, result.Status);
        Assert.Equal(AgentTraceCaptureState.Unavailable, result.CaptureState);
    }

    [Fact]
    public void Registry_LookupIsCaseSensitiveAndOrdinal()
    {
        Assert.True(_registry.TryGet(AgentBackendIds.NativeHarnessValue, out var nh));
        Assert.True(_registry.TryGet(AgentBackendIds.AcpValue, out var acp));
        Assert.False(_registry.TryGet("BACKEND:ZAIDE-NATIVE-HARNESS", out _));
        Assert.False(_registry.TryGet(string.Empty, out _));
        Assert.Equal(2, _registry.All.Count);
    }

    [Fact]
    public void BackendEvidenceSourceWriter_RoutesThroughCoordinatorEnvelope()
    {
        // The writer passes the raw payload to the coordinator; the capture
        // sink wraps it in the M1 Trace envelope. The stored record always
        // carries the typed envelope so the inspector can decode it.
        var request = Phase21TraceTestSupport.CreateRequest(
            _workspaceKey,
            AgentBackendIds.NativeHarnessValue,
            AgentTraceKind.Request,
            "{\"method\":\"ping\"}",
            idempotencyKey: "wrap-1");

        var result = _writer.Submit(request, evidenceLevel: AgentTraceEvidenceLevel.BackendExecutedAndReported);

        Assert.Equal(AgentTraceCaptureStatus.Accepted, result.Status);
        Phase21TraceTestSupport.WaitForQueueDrain(_queue, expectedWritten: 1);
        var records = Phase21TraceTestSupport.ReplayTraceRecords(_store, _workspaceKey);
        var single = Assert.Single(records);
        Assert.Contains("\"backendId\":", single.PayloadJson);
        Assert.Contains("\"kind\":", single.PayloadJson);
        Assert.Contains("\"redactedPayload\":", single.PayloadJson);
    }

    [Fact]
    public void NativeHarnessSource_CanReportUnavailable()
    {
        var request = Phase21TraceTestSupport.CreateRequest(
            _workspaceKey,
            AgentBackendIds.NativeHarnessValue,
            AgentTraceKind.UnavailableMarker,
            string.Empty,
            idempotencyKey: "nh-unavailable");

        var result = _nativeHarnessSource.Submit(request);

        Assert.Equal(AgentTraceCaptureStatus.Unavailable, result.Status);
        Assert.Equal(AgentTraceCaptureState.Unavailable, result.CaptureState);
    }
}
