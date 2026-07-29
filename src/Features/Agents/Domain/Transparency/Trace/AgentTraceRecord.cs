using System;

namespace Zaide.Features.Agents.Domain.Transparency.Trace;

/// <summary>
/// One redacted durable trace record admitted to the M1 Trace record class.
/// Constructed only by the capture pipeline after redaction, queueing, and
/// durable persistence; inspection reads these values without re-reading the
/// original input payload.
/// </summary>
internal sealed class AgentTraceRecord
{
    public AgentTraceRecord(
        string recordId,
        long orderingSequence,
        string backendId,
        AgentTraceKind kind,
        AgentTraceEvidenceLevel evidenceLevel,
        AgentTraceCaptureState captureState,
        string redactedPayloadJson,
        int payloadByteCount,
        AgentTraceRecordScope scope,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset recordedAtUtc,
        string? redactionReason)
    {
        if (string.IsNullOrWhiteSpace(recordId))
        {
            throw new ArgumentException("Record id is required.", nameof(recordId));
        }

        if (orderingSequence < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(orderingSequence),
                orderingSequence,
                "Ordering sequence must be positive.");
        }

        if (string.IsNullOrWhiteSpace(backendId))
        {
            throw new ArgumentException("Backend id is required.", nameof(backendId));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Trace kind is invalid.");
        }

        if (!Enum.IsDefined(evidenceLevel))
        {
            throw new ArgumentOutOfRangeException(
                nameof(evidenceLevel),
                evidenceLevel,
                "Evidence level is invalid.");
        }

        if (!Enum.IsDefined(captureState))
        {
            throw new ArgumentOutOfRangeException(
                nameof(captureState),
                captureState,
                "Capture state is invalid.");
        }

        if (string.IsNullOrEmpty(redactedPayloadJson))
        {
            throw new ArgumentException(
                "Redacted payload is required.",
                nameof(redactedPayloadJson));
        }

        if (payloadByteCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(payloadByteCount),
                payloadByteCount,
                "Payload byte count must be non-negative.");
        }

        RecordId = recordId;
        OrderingSequence = orderingSequence;
        BackendId = backendId;
        Kind = kind;
        EvidenceLevel = evidenceLevel;
        CaptureState = captureState;
        RedactedPayloadJson = redactedPayloadJson;
        PayloadByteCount = payloadByteCount;
        Scope = scope;
        CapturedAtUtc = capturedAtUtc;
        RecordedAtUtc = recordedAtUtc;
        RedactionReason = redactionReason;
    }

    public string RecordId { get; }

    public long OrderingSequence { get; }

    public string BackendId { get; }

    public AgentTraceKind Kind { get; }

    public AgentTraceEvidenceLevel EvidenceLevel { get; }

    public AgentTraceCaptureState CaptureState { get; }

    /// <summary>Always post-redaction JSON. Never the original payload.</summary>
    public string RedactedPayloadJson { get; }

    public int PayloadByteCount { get; }

    public AgentTraceRecordScope Scope { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public DateTimeOffset RecordedAtUtc { get; }

    public string? RedactionReason { get; }
}
