using System;
using System.Threading;
using Zaide.Features.Agents.Application.Continuity;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Contracts.Continuity;
using Zaide.Features.Agents.Domain.Continuity;

namespace Zaide.Features.Agents.Application.Continuity;

internal sealed class AgentSessionContinuityStartupReconciler
{
    private readonly IAgentSessionContinuityCoordinator _coordinator;
    private readonly AgentDurableWorkspaceStorageKeyResolver _workspaceKeyResolver;
    private readonly AgentSessionContinuityLegacyCwdReader _legacyCwdReader;
    private readonly AgentSessionContinuityConversationProjector _conversationProjector;
    private readonly Func<string?> _legacyCwdRootProvider;
    private int _startupReconciled;

    public AgentSessionContinuityStartupReconciler(
        IAgentSessionContinuityCoordinator coordinator,
        AgentDurableWorkspaceStorageKeyResolver workspaceKeyResolver,
        AgentSessionContinuityLegacyCwdReader legacyCwdReader,
        AgentSessionContinuityConversationProjector conversationProjector,
        Func<string?>? legacyCwdRootProvider = null)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _workspaceKeyResolver = workspaceKeyResolver
            ?? throw new ArgumentNullException(nameof(workspaceKeyResolver));
        _legacyCwdReader = legacyCwdReader ?? throw new ArgumentNullException(nameof(legacyCwdReader));
        _conversationProjector = conversationProjector
            ?? throw new ArgumentNullException(nameof(conversationProjector));
        _legacyCwdRootProvider = legacyCwdRootProvider
            ?? AgentContinuityWorkspaceRootProvider.CreateLegacyProcessCwdProvider();
    }

    public AgentSessionContinuityReconcileSummary ReconcileOnStartupIfNeeded()
    {
        if (Interlocked.CompareExchange(ref _startupReconciled, 1, 0) != 0)
        {
            return new AgentSessionContinuityReconcileSummary(
                0,
                0,
                0,
                Array.Empty<AgentSessionContinuityInterruptedSession>());
        }

        var legacyRoot = _legacyCwdRootProvider();
        if (string.IsNullOrWhiteSpace(legacyRoot))
        {
            return new AgentSessionContinuityReconcileSummary(
                0,
                0,
                0,
                Array.Empty<AgentSessionContinuityInterruptedSession>());
        }

        var legacySummary = _legacyCwdReader.ReadLegacyCwdInterruptedSessions();
        if (legacySummary.InterruptedSessions.Count == 0)
        {
            return legacySummary;
        }

        var legacyKey = _workspaceKeyResolver.Resolve(legacyRoot);
        var summary = _coordinator.Reconcile(new AgentSessionContinuityReconcileRequest(
            legacyKey,
            legacyRoot,
            isStartup: true,
            origin: AgentSessionContinuityReconcileOrigin.StartupLegacyCwd));

        _conversationProjector.ProjectReconcileSummary(
            summary,
            AgentSessionContinuityReconcileOrigin.StartupLegacyCwd);

        return summary;
    }
}
