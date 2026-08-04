using System;
using System.Collections.Concurrent;
using System.Linq;
using Zaide.Features.Agents.Application.Continuity;
using Zaide.Features.Agents.Domain.Continuity;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Projects classified interrupted sessions into the owning conversation through
/// <see cref="AgentConversationEventProjection"/> only. Classification never
/// invokes backends or replays prior permission/proposal state.
/// </summary>
internal sealed class AgentSessionContinuityConversationProjector
{
    private readonly IConversationStore _conversationStore;
    private readonly IActorCatalog? _actorCatalog;
    private readonly ConcurrentDictionary<string, byte> _projectedKeys = new(StringComparer.Ordinal);

    public AgentSessionContinuityConversationProjector(
        IConversationStore conversationStore,
        IActorCatalog? actorCatalog = null)
    {
        _conversationStore = conversationStore ?? throw new ArgumentNullException(nameof(conversationStore));
        _actorCatalog = actorCatalog;
    }

    public void ProjectReconcileSummary(
        AgentSessionContinuityReconcileSummary summary,
        AgentSessionContinuityReconcileOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(summary);

        var isLegacy = origin == AgentSessionContinuityReconcileOrigin.StartupLegacyCwd;
        foreach (var interrupted in summary.InterruptedSessions)
        {
            ProjectInterruptedSession(interrupted, origin, isLegacy);
        }
    }

    public void ProjectInterruptedSession(
        AgentSessionContinuityInterruptedSession interrupted,
        AgentSessionContinuityReconcileOrigin origin,
        bool isLegacyCwdRecord)
    {
        ArgumentNullException.ThrowIfNull(interrupted);

        var projectionKey = BuildProjectionKey(
            origin,
            interrupted.Scope.SessionId.Value,
            interrupted.Classification.ToString(),
            interrupted.LatestCheckpoint.RecordedAtUtc.UtcTicks.ToString());

        if (!_projectedKeys.TryAdd(projectionKey, 0))
        {
            return;
        }

        if (!_conversationStore.TryGet(interrupted.Scope.ConversationId, out var conversation))
        {
            return;
        }

        var author = ResolveAgentAuthor(conversation);
        AgentConversationEventProjection.ProjectInterruptedRun(
            _conversationStore,
            interrupted.Scope.ConversationId,
            author,
            interrupted.Scope.SessionId,
            interrupted.Classification,
            interrupted.LatestCheckpoint.RunStatus,
            isLegacyCwdRecord,
            origin);
    }

    private ActorId ResolveAgentAuthor(Conversation conversation)
    {
        var humanId = _actorCatalog?.CanonicalHuman.Id ?? ActorId.HumanUser;
        var peer = conversation.Participants.All.FirstOrDefault(p => p != humanId);
        if (peer != default)
        {
            return peer;
        }

        return _actorCatalog?.CanonicalTownhallAgent.Id ?? ActorId.TownhallAgent;
    }

    private static string BuildProjectionKey(
        AgentSessionContinuityReconcileOrigin origin,
        string sessionId,
        string classification,
        string recordedTicks) =>
        $"{origin}|{sessionId}|{classification}|{recordedTicks}";
}
