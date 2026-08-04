using System;
using System.Text;
using Zaide.Features.Agents.Contracts.Transparency.Trace;
using Zaide.Features.Agents.Domain.Transparency.Trace;

namespace Zaide.Features.Agents.Application.Transparency.Trace;

internal interface IAgentTraceBackendEvidenceSourceInitializable
{
    void Initialize(AgentTraceBackendEvidenceSourceWriter writer);
}

/// <summary>
/// Helper shared by every backend evidence source. Routes the source's
/// neutral payload through the coordinator so backend adapters never touch
/// the capture pipeline internals or the M1 record store directly. The
/// source is responsible for the evidence level claim; the coordinator
/// owns redaction, queueing, envelope serialization, and durable
/// persistence.
/// </summary>
internal sealed class AgentTraceBackendEvidenceSourceWriter
{
    private readonly AgentTraceCoordinator _coordinator;

    public AgentTraceBackendEvidenceSourceWriter(
        AgentTraceCoordinator coordinator)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public AgentTraceCaptureResult Submit(
        AgentTraceCaptureRequest request,
        AgentTraceEvidenceLevel evidenceLevel)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rewritten = new AgentTraceCaptureRequest(
            request.WorkspaceKey,
            request.BackendId,
            request.Kind,
            evidenceLevel,
            request.PayloadJson,
            request.Scope,
            request.IdempotencyKey,
            request.CapturedAtUtc);

        return _coordinator.TrySubmit(rewritten);
    }

    public static string ComputeOpaqueBodyMarker(string body)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(body));
        return Convert.ToBase64String(hash);
    }
}
