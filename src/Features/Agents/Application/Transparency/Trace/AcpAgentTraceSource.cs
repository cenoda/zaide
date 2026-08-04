using System;
using System.Text.Json;
using Zaide.Features.Agents.Contracts.Transparency.Trace;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Trace;

namespace Zaide.Features.Agents.Application.Transparency.Trace;

/// <summary>
/// Narrow ACP evidence adapter. Produces neutral trace inputs derived from
/// the public ACP protocol frame envelope (method, id, kind) without sharing
/// backend-private internals. The JSON-RPC params and result bodies are
/// serialized only as opaque base64 markers so the redaction processor
/// defensively scans them; the raw frame is not duplicated as a secret.
/// </summary>
internal sealed class AcpAgentTraceSource
    : IAgentTraceBackendEvidenceSource, IAgentTraceBackendEvidenceSourceInitializable
{
    private AgentTraceBackendEvidenceSourceWriter? _writer;

    public AcpAgentTraceSource()
    {
    }

    public AcpAgentTraceSource(AgentTraceBackendEvidenceSourceWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public string BackendId => AgentBackendIds.AcpValue;

    public bool CanExpose(AgentTraceKind kind) => kind switch
    {
        AgentTraceKind.Request => true,
        AgentTraceKind.Response => true,
        AgentTraceKind.ProtocolFrame => true,
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
            return new AgentTraceCaptureResult(
                AgentTraceCaptureStatus.Disabled,
                captureState: AgentTraceCaptureState.Unavailable,
                reason: "acp-cannot-expose-kind");
        }

        return _writer?.Submit(request, evidenceLevel: AgentTraceEvidenceLevel.BackendExecutedAndReported)
            ?? new AgentTraceCaptureResult(
                AgentTraceCaptureStatus.Disabled,
                captureState: AgentTraceCaptureState.Unavailable,
                reason: "acp-trace-source-not-initialized");
    }

    public void Initialize(AgentTraceBackendEvidenceSourceWriter writer) =>
        _writer ??= writer ?? throw new ArgumentNullException(nameof(writer));

    /// <summary>
    /// Serializes one ACP protocol frame into a neutral trace evidence JSON.
    /// The <paramref name="opaqueBodyBase64"/> parameter is the SHA-256-derived
    /// opaque body marker, never the raw frame body, so the source does not
    /// share backend-private internals.
    /// </summary>
    public static string SerializeProtocolFrame(
        string backendId,
        string method,
        string? id,
        string direction,
        DateTimeOffset observedAtUtc,
        string opaqueBodyBase64)
    {
        var payload = new AcpProtocolFrameJson
        {
            Backend = backendId,
            Method = method,
            Id = id ?? string.Empty,
            Direction = direction,
            ObservedAtUtc = observedAtUtc,
            OpaqueBodyBase64 = opaqueBodyBase64,
        };
        return JsonSerializer.Serialize(payload, AcpProtocolFrameSerializerOptions);
    }

    private static readonly JsonSerializerOptions AcpProtocolFrameSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private sealed class AcpProtocolFrameJson
    {
        public string Backend { get; set; } = string.Empty;

        public string Method { get; set; } = string.Empty;

        public string Id { get; set; } = string.Empty;

        public string Direction { get; set; } = string.Empty;

        public DateTimeOffset ObservedAtUtc { get; set; }

        public string OpaqueBodyBase64 { get; set; } = string.Empty;
    }
}
