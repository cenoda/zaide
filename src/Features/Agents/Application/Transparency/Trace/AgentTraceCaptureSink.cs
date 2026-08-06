using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using Zaide.Features.Agents.Contracts.Transparency.Trace;
using Zaide.Features.Agents.Domain.Transparency.Trace;

namespace Zaide.Features.Agents.Application.Transparency.Trace;

/// <summary>
/// Pipeline owner for the M2 trace evidence flow. Each
/// <see cref="TrySubmit"/> call runs mandatory redaction, applies bounded
/// payload enforcement, wraps the redacted content in the typed trace
/// envelope, then enqueues the wrapped payload for nonblocking durable
/// persistence through the M1 Trace record class. The capture pipeline
/// never admits an unredacted payload and never blocks the agent event
/// pipeline.
/// </summary>
internal sealed class AgentTraceCaptureSink : IAgentTraceCaptureSink
{
    private static readonly JsonSerializerOptions EnvelopeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly AgentTraceCaptureLimits _limits;
    private readonly AgentTraceBoundedCaptureQueue _queue;
    private readonly AgentDurableWorkspaceStorageKeyResolver _workspaceKeyResolver;
    private readonly IAgentTraceBackendEvidenceSourceRegistryFilter _sourceFilter;
    private int _captureEnabledCounter;

    public AgentTraceCaptureSink(
        AgentTraceCaptureLimits limits,
        AgentTraceBoundedCaptureQueue queue,
        AgentDurableWorkspaceStorageKeyResolver workspaceKeyResolver,
        IAgentTraceBackendEvidenceSourceRegistryFilter? sourceFilter = null)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(workspaceKeyResolver);

        _limits = limits;
        _queue = queue;
        _workspaceKeyResolver = workspaceKeyResolver;
        _sourceFilter = sourceFilter
            ?? AllowAllAgentTraceBackendEvidenceSourceRegistryFilter.Instance;
    }

    public AgentTraceCaptureLimits Limits => _limits;

    public AgentTraceBoundedCaptureQueue Queue => _queue;

    public long BackpressureDroppedCount => _queue.DroppedCount;

    public long AdmittedCount => _queue.AdmittedCount;

    public long WrittenCount => _queue.WrittenCount;

    public bool IsCaptureEnabled() => Volatile.Read(ref _captureEnabledCounter) > 0;

    public void EnableCapture() => Interlocked.Increment(ref _captureEnabledCounter);

    public void DisableCapture() => Interlocked.Exchange(ref _captureEnabledCounter, 0);

    /// <summary>
    /// Sets capture on or off from durable settings (idempotent).
    /// </summary>
    public void ApplyCaptureEnabled(bool enabled) =>
        Interlocked.Exchange(ref _captureEnabledCounter, enabled ? 1 : 0);

    public AgentTraceCaptureResult TrySubmit(AgentTraceCaptureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsCaptureEnabled())
        {
            return new AgentTraceCaptureResult(
                AgentTraceCaptureStatus.Disabled,
                captureState: AgentTraceCaptureState.Disabled,
                reason: "Trace capture is disabled.");
        }

        if (!_sourceFilter.IsAllowed(request.BackendId))
        {
            return new AgentTraceCaptureResult(
                AgentTraceCaptureStatus.Disabled,
                captureState: AgentTraceCaptureState.Disabled,
                reason: "Backend is not a registered trace source.");
        }

        if (request.Kind == AgentTraceKind.UnavailableMarker)
        {
            return Admit(
                request,
                redactedPayload: "{\"state\":\"unavailable\"}",
                captureState: AgentTraceCaptureState.Unavailable,
                status: AgentTraceCaptureStatus.Unavailable,
                redactionReason: null);
        }

        if (request.PayloadJson.Length == 0)
        {
            return new AgentTraceCaptureResult(
                AgentTraceCaptureStatus.InvalidRequest,
                reason: "Empty payload.");
        }

        var redaction = AgentTraceRedactionProcessor.Apply(request.PayloadJson);
        if (redaction.DidProcessingFail)
        {
            return Admit(
                request,
                redactedPayload: redaction.Content,
                captureState: AgentTraceCaptureState.Failed,
                status: AgentTraceCaptureStatus.RedactionFailed,
                redactionReason: "redaction-processing-failed");
        }

        if (redaction.ByteCount > _limits.MaxPayloadBytes)
        {
            var truncated = TruncateForBound(redaction.Content);
            return Admit(
                request,
                redactedPayload: truncated,
                captureState: AgentTraceCaptureState.Truncated,
                status: AgentTraceCaptureStatus.Truncated,
                redactionReason: $"payload-exceeds-{_limits.MaxPayloadBytes}");
        }

        return Admit(
            request,
            redactedPayload: redaction.Content,
            captureState: redaction.State,
            status: AgentTraceCaptureStatus.Accepted,
            redactionReason: redaction.Reason?.SecretClass);
    }

    private AgentTraceCaptureResult Admit(
        AgentTraceCaptureRequest request,
        string redactedPayload,
        AgentTraceCaptureState captureState,
        AgentTraceCaptureStatus status,
        string? redactionReason)
    {
        var idempotencyKey = request.IdempotencyKey
            ?? BuildIdempotencyKey(request);

        var envelope = new TraceRecordEnvelope
        {
            BackendId = request.BackendId,
            Kind = request.Kind,
            EvidenceLevel = request.EvidenceLevel,
            CaptureState = captureState,
            RedactedPayload = redactedPayload,
            PayloadByteCount = Encoding.UTF8.GetByteCount(redactedPayload),
            CapturedAtUtc = request.CapturedAtUtc,
            RedactionReason = redactionReason,
        };
        var envelopeJson = JsonSerializer.Serialize(envelope, EnvelopeOptions);

        var item = new AgentTraceBoundedCaptureItem(
            request.WorkspaceKey,
            request.BackendId,
            idempotencyKey,
            envelopeJson,
            request.Scope,
            request.CapturedAtUtc);

        if (_queue.TryEnqueue(item) == false)
        {
            return new AgentTraceCaptureResult(
                AgentTraceCaptureStatus.BackpressureRejected,
                captureState: captureState,
                reason: "Capture queue is full.");
        }

        return new AgentTraceCaptureResult(
            status,
            captureState: captureState,
            reason: redactionReason);
    }

    private string TruncateForBound(string content)
    {
        var maxChars = Math.Max(0, _limits.MaxPayloadBytes - 32);
        if (content.Length <= maxChars)
        {
            return content;
        }

        var marker = "{\"state\":\"truncated\"}";
        var kept = content.Substring(0, maxChars - marker.Length);
        return kept + marker;
    }

    private static string BuildIdempotencyKey(AgentTraceCaptureRequest request)
    {
        var raw = string.Join(
            "|",
            request.WorkspaceKey.Value,
            request.BackendId,
            request.Kind.ToString(),
            request.CapturedAtUtc.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return "trace:" + Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..16];
    }

    /// <summary>
    /// Wire format for one trace row stored in the M1 Trace record class.
    /// Stable across schema versions. The capture pipeline always writes
    /// this shape so the inspector can decode without re-reading the
    /// original input payload.
    /// </summary>
    private sealed class TraceRecordEnvelope
    {
        public string BackendId { get; set; } = string.Empty;

        public AgentTraceKind Kind { get; set; }

        public AgentTraceEvidenceLevel EvidenceLevel { get; set; }

        public AgentTraceCaptureState CaptureState { get; set; }

        public string RedactedPayload { get; set; } = string.Empty;

        public int PayloadByteCount { get; set; }

        public DateTimeOffset CapturedAtUtc { get; set; }

        public string? RedactionReason { get; set; }
    }
}

/// <summary>
/// Optional filter that constrains which backends may submit trace evidence.
/// Composition wires this so a future settings toggle can disable capture for
/// specific backends without re-instantiating the sink.
/// </summary>
internal interface IAgentTraceBackendEvidenceSourceRegistryFilter
{
    bool IsAllowed(string backendId);
}

/// <summary>
/// Default filter that admits any registered backend id.
/// </summary>
internal sealed class AllowAllAgentTraceBackendEvidenceSourceRegistryFilter
    : IAgentTraceBackendEvidenceSourceRegistryFilter
{
    public static AllowAllAgentTraceBackendEvidenceSourceRegistryFilter Instance { get; } = new();

    public bool IsAllowed(string backendId) => !string.IsNullOrWhiteSpace(backendId);
}
