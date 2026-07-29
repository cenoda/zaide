using System;

namespace Zaide.Features.Agents.Domain.Transparency.Trace;

/// <summary>
/// One neutral backend-exposed trace evidence input submitted to the capture
/// pipeline. The submitted <see cref="PayloadJson"/> is the deepest truthful
/// layer the backend exposed; the sink runs mandatory redaction and bounded
/// admission before any durable write.
/// </summary>
internal sealed class AgentTraceCaptureRequest
{
    public AgentTraceCaptureRequest(
        AgentDurableWorkspaceStorageKey workspaceKey,
        string backendId,
        AgentTraceKind kind,
        AgentTraceEvidenceLevel evidenceLevel,
        string payloadJson,
        AgentTraceRecordScope scope,
        string? idempotencyKey = null,
        DateTimeOffset? capturedAtUtc = null)
    {
        if (workspaceKey.Value.Length == 0)
        {
            throw new ArgumentException("Workspace key is required.", nameof(workspaceKey));
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

        WorkspaceKey = workspaceKey;
        BackendId = backendId;
        Kind = kind;
        EvidenceLevel = evidenceLevel;
        PayloadJson = payloadJson ?? string.Empty;
        Scope = scope;
        IdempotencyKey = idempotencyKey;
        CapturedAtUtc = capturedAtUtc ?? DateTimeOffset.UtcNow;
    }

    public AgentDurableWorkspaceStorageKey WorkspaceKey { get; }

    public string BackendId { get; }

    public AgentTraceKind Kind { get; }

    public AgentTraceEvidenceLevel EvidenceLevel { get; }

    public string PayloadJson { get; }

    public AgentTraceRecordScope Scope { get; }

    public string? IdempotencyKey { get; }

    public DateTimeOffset CapturedAtUtc { get; }
}
