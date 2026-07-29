using Zaide.Features.Agents.Domain.Transparency.Trace;

namespace Zaide.Features.Agents.Contracts.Transparency.Trace;

/// <summary>
/// Backend-neutral submission seam for one backend's deepest exposed trace
/// layer. Each production backend (Native Harness, ACP) implements a narrow
/// adapter that produces neutral trace inputs without sharing
/// backend-private internals with the capture pipeline.
/// </summary>
internal interface IAgentTraceBackendEvidenceSource
{
    /// <summary>
    /// Stable backend identifier owning this evidence source. Must match the
    /// backend's <see cref="Zaide.Features.Agents.Domain.AgentBackendId"/>.
    /// </summary>
    string BackendId { get; }

    /// <summary>
    /// Reports whether this backend can currently expose the requested trace
    /// kind. False yields an Unavailable marker without redaction work.
    /// </summary>
    bool CanExpose(AgentTraceKind kind);

    /// <summary>
    /// Submits one neutral trace capture request to the sink. The source
    /// owns serialization of its exposed layer into a <see cref="AgentTraceCaptureRequest"/>;
    /// redaction, queueing, and durable persistence remain sink responsibilities.
    /// </summary>
    AgentTraceCaptureResult Submit(AgentTraceCaptureRequest request);
}
