namespace Zaide.Features.Agents.Domain.Transparency.Trace;

/// <summary>
/// Evidence level for one redacted trace row. Distinct from the per-event
/// <see cref="Zaide.Features.Agents.Domain.AgentActivityEvidenceLevel"/>; trace
/// evidence describes what the backend exposed, not what the agent did.
/// </summary>
internal enum AgentTraceEvidenceLevel
{
    /// <summary>Trace row was synthesized by Zaide (for example, an unavailable marker).</summary>
    ZaideExecuted = 0,
    /// <summary>Trace row passed through Zaide-mediated redaction.</summary>
    ZaideMediated = 1,
    /// <summary>Trace row was produced by the backend and reported to Zaide.</summary>
    BackendExecutedAndReported = 2,
    /// <summary>Trace row was observed externally and forwarded to Zaide.</summary>
    ExternallyObserved = 3,
    /// <summary>Trace row cannot be inspected or verified.</summary>
    Unobservable = 4,
}
