using System;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Contracts.Continuity;
using Zaide.Features.Agents.Domain.Continuity;
using Zaide.Features.Agents.Domain.Transparency;

namespace Zaide.Features.Agents.Application.Continuity;

/// <summary>
/// Read-only compatibility access to session-recovery records keyed by the
/// legacy process CWD partition. Records are never merged, migrated, rewritten,
/// or deleted by this reader.
/// </summary>
internal sealed class AgentSessionContinuityLegacyCwdReader
{
    private readonly IAgentSessionContinuityInspector _inspector;
    private readonly AgentDurableWorkspaceStorageKeyResolver _workspaceKeyResolver;
    private readonly Func<string?> _legacyRootProvider;

    public AgentSessionContinuityLegacyCwdReader(
        IAgentSessionContinuityInspector inspector,
        AgentDurableWorkspaceStorageKeyResolver workspaceKeyResolver,
        Func<string?>? legacyRootProvider = null)
    {
        _inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
        _workspaceKeyResolver = workspaceKeyResolver
            ?? throw new ArgumentNullException(nameof(workspaceKeyResolver));
        _legacyRootProvider = legacyRootProvider
            ?? AgentContinuityWorkspaceRootProvider.CreateLegacyProcessCwdProvider();
    }

    public AgentSessionContinuityReconcileSummary ReadLegacyCwdInterruptedSessions()
    {
        var legacyRoot = _legacyRootProvider();
        if (string.IsNullOrWhiteSpace(legacyRoot))
        {
            return new AgentSessionContinuityReconcileSummary(
                0,
                0,
                0,
                Array.Empty<AgentSessionContinuityInterruptedSession>());
        }

        var legacyKey = _workspaceKeyResolver.Resolve(legacyRoot);
        return _inspector.GetInterruptedSessions(legacyKey, legacyRoot);
    }
}
