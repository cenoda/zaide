using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Zaide.Features.Agents.Application.Continuity;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Contracts.Continuity;
using Zaide.Features.Agents.Domain.Continuity;

namespace Zaide.Features.Agents.Application.Continuity;

/// <summary>
/// Application-start compatibility path for legacy process-CWD continuity
/// partitions. May inspect, classify, and project only. Never appends,
/// rewrites, migrates, merges, deletes, or compacts the legacy partition.
/// Workspace-open reconciliation remains the writable workspace-owned path.
/// </summary>
internal sealed class AgentSessionContinuityStartupReconciler
{
    private readonly AgentSessionContinuityRevalidator _revalidator;
    private readonly AgentDurableWorkspaceStorageKeyResolver _workspaceKeyResolver;
    private readonly AgentSessionContinuityLegacyCwdReader _legacyCwdReader;
    private readonly AgentSessionContinuityConversationProjector _conversationProjector;
    private readonly Func<string?> _legacyCwdRootProvider;
    private int _startupReconciled;

    public AgentSessionContinuityStartupReconciler(
        AgentSessionContinuityRevalidator revalidator,
        AgentDurableWorkspaceStorageKeyResolver workspaceKeyResolver,
        AgentSessionContinuityLegacyCwdReader legacyCwdReader,
        AgentSessionContinuityConversationProjector conversationProjector,
        Func<string?>? legacyCwdRootProvider = null)
    {
        _revalidator = revalidator ?? throw new ArgumentNullException(nameof(revalidator));
        _workspaceKeyResolver = workspaceKeyResolver
            ?? throw new ArgumentNullException(nameof(workspaceKeyResolver));
        _legacyCwdReader = legacyCwdReader ?? throw new ArgumentNullException(nameof(legacyCwdReader));
        _conversationProjector = conversationProjector
            ?? throw new ArgumentNullException(nameof(conversationProjector));
        _legacyCwdRootProvider = legacyCwdRootProvider
            ?? AgentContinuityWorkspaceRootProvider.CreateLegacyProcessCwdProvider();
    }

    /// <summary>
    /// One-shot legacy classify and project. Does not call
    /// <see cref="IAgentSessionContinuityCoordinator.Reconcile"/> and does not
    /// mutate durable records under the legacy partition.
    /// </summary>
    public AgentSessionContinuityReconcileSummary ReconcileOnStartupIfNeeded()
    {
        if (Interlocked.CompareExchange(ref _startupReconciled, 1, 0) != 0)
        {
            return EmptySummary();
        }

        var legacyRoot = _legacyCwdRootProvider();
        if (string.IsNullOrWhiteSpace(legacyRoot))
        {
            return EmptySummary();
        }

        var legacySummary = _legacyCwdReader.ReadLegacyCwdInterruptedSessions();
        if (legacySummary.InterruptedSessions.Count == 0)
        {
            return legacySummary;
        }

        // Classify in memory only. Keep original checkpoint payloads; never
        // append AfterStartupReconcile (or any other) records to the legacy partition.
        var reclassified = new List<AgentSessionContinuityInterruptedSession>(
            legacySummary.InterruptedSessions.Count);

        foreach (var interrupted in legacySummary.InterruptedSessions)
        {
            var classification = _revalidator.ClassifyCheckpoint(
                interrupted.LatestCheckpoint,
                legacyRoot);

            reclassified.Add(new AgentSessionContinuityInterruptedSession(
                interrupted.Scope,
                classification,
                interrupted.LatestCheckpoint,
                interrupted.ResumeAdmitted,
                interrupted.Terminated));
        }

        var summary = new AgentSessionContinuityReconcileSummary(
            reclassified.Count(item => item.Classification == AgentSessionContinuityClassification.Recoverable),
            legacySummary.TerminalCount,
            reclassified.Count(item => item.Classification == AgentSessionContinuityClassification.Indeterminate),
            reclassified);

        _conversationProjector.ProjectReconcileSummary(
            summary,
            AgentSessionContinuityReconcileOrigin.StartupLegacyCwd);

        return summary;
    }

    private static AgentSessionContinuityReconcileSummary EmptySummary() =>
        new(
            0,
            0,
            0,
            Array.Empty<AgentSessionContinuityInterruptedSession>());
}
