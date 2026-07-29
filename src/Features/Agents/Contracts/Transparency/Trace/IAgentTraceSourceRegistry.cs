using System.Collections.Generic;

namespace Zaide.Features.Agents.Contracts.Transparency.Trace;

/// <summary>
/// Registry of backend-neutral trace evidence sources. Composition root
/// registers one source per production backend.
/// </summary>
internal interface IAgentTraceSourceRegistry
{
    void Register(IAgentTraceBackendEvidenceSource source);

    bool TryGet(string backendId, out IAgentTraceBackendEvidenceSource source);

    IReadOnlyList<IAgentTraceBackendEvidenceSource> All { get; }
}
