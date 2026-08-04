using System;
using System.Text.Json;
using Zaide.Features.Agents.Contracts.Transparency.Trace;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Trace;

namespace Zaide.Features.Agents.Application.Transparency.Trace;

/// <summary>
/// Narrow Native Harness evidence adapter. Produces neutral trace inputs
/// derived from the existing backend-public loop history records. The source
/// never re-exposes backend-private loop internals; each submission carries
/// only the loop history turn index, kind, timestamp, and the public text
/// surface already admitted to the conversation projection.
/// </summary>
internal sealed class NativeHarnessAgentTraceSource
    : IAgentTraceBackendEvidenceSource, IAgentTraceBackendEvidenceSourceInitializable
{
    private AgentTraceBackendEvidenceSourceWriter? _writer;

    public NativeHarnessAgentTraceSource()
    {
    }

    public NativeHarnessAgentTraceSource(AgentTraceBackendEvidenceSourceWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public string BackendId => AgentBackendIds.NativeHarnessValue;

    public bool CanExpose(AgentTraceKind kind) => kind switch
    {
        AgentTraceKind.Request => true,
        AgentTraceKind.Response => true,
        AgentTraceKind.ToolCall => true,
        AgentTraceKind.ToolResult => true,
        AgentTraceKind.BackendLoopHistory => true,
        AgentTraceKind.Error => true,
        AgentTraceKind.CapabilityDiscovery => true,
        AgentTraceKind.UnavailableMarker => true,
        _ => false,
    };

    public AgentTraceCaptureResult Submit(AgentTraceCaptureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!CanExpose(request.Kind))
        {
            return Reject(request, "native-harness-cannot-expose-kind");
        }

        return _writer?.Submit(request, evidenceLevel: AgentTraceEvidenceLevel.BackendExecutedAndReported)
            ?? new AgentTraceCaptureResult(
                AgentTraceCaptureStatus.Disabled,
                captureState: AgentTraceCaptureState.Unavailable,
                reason: "native-harness-trace-source-not-initialized");
    }

    public void Initialize(AgentTraceBackendEvidenceSourceWriter writer) =>
        _writer ??= writer ?? throw new ArgumentNullException(nameof(writer));

    private static AgentTraceCaptureResult Reject(AgentTraceCaptureRequest request, string reason) =>
        new(
            AgentTraceCaptureStatus.Disabled,
            captureState: AgentTraceCaptureState.Unavailable,
            reason: reason);

    /// <summary>
    /// Serializes one Native Harness loop history turn into a neutral trace
    /// evidence JSON. Stable shape: backend, kind, turnIndex, recordedAtUtc,
    /// and the admitted public text surface. Secrets, tool arguments, and
    /// model tool names are not serialized; the redaction processor still
    /// scans the JSON defensively.
    /// </summary>
    public static string SerializeLoopHistoryTurn(
        string backendId,
        string kindLabel,
        int turnIndex,
        DateTimeOffset recordedAtUtc,
        string publicText)
    {
        var payload = new LoopHistoryTurnJson
        {
            Backend = backendId,
            Kind = kindLabel,
            TurnIndex = turnIndex,
            RecordedAtUtc = recordedAtUtc,
            PublicText = publicText,
        };
        return JsonSerializer.Serialize(payload, LoopHistorySerializerOptions);
    }

    private static readonly JsonSerializerOptions LoopHistorySerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private sealed class LoopHistoryTurnJson
    {
        public string Backend { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public int TurnIndex { get; set; }

        public DateTimeOffset RecordedAtUtc { get; set; }

        public string PublicText { get; set; } = string.Empty;
    }
}
