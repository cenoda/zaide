using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Routing orchestration seam. Resolves <c>@mention</c> targets against the
/// typed actor catalog roster (not open panel tabs), get-or-creates a thin panel
/// host for the target conversation, and dispatches execution. Direct-send and
/// routing-failure outcomes remain attached to the owning <see cref="ConversationId"/>.
/// </summary>
public sealed class AgentRouter : IAgentRouter
{
    private readonly MentionParser _parser;
    private readonly IAgentPanelHost _panelHost;
    private readonly IAgentExecutionCoordinator _coordinator;
    private readonly IActorCatalog _actorCatalog;
    private readonly IConversationStore _conversationStore;

    public AgentRouter(
        MentionParser parser,
        IAgentPanelHost panelHost,
        IAgentExecutionCoordinator coordinator,
        IActorCatalog actorCatalog,
        IConversationStore conversationStore)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _panelHost = panelHost ?? throw new ArgumentNullException(nameof(panelHost));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _actorCatalog = actorCatalog ?? throw new ArgumentNullException(nameof(actorCatalog));
        _conversationStore = conversationStore ?? throw new ArgumentNullException(nameof(conversationStore));
    }

    public Task<RouteResult> RouteAndExecuteAsync(
        string sourcePanelId,
        string rawInput,
        CancellationToken ct = default)
    {
        var sourcePanel = _panelHost.Panels.FirstOrDefault(p => p.PanelId == sourcePanelId);
        if (sourcePanel is null)
        {
            return Task.FromResult(CreateRoutingFailureRouteResult(null, null, "Unknown source panel"));
        }

        return RouteAndExecuteCoreAsync(
            sourcePanel.ConversationId,
            sourcePanel,
            sourcePanelId,
            rawInput,
            ct);
    }

    public Task<RouteResult> RouteAndExecuteFromConversationAsync(
        ConversationId sourceConversationId,
        string rawInput,
        CancellationToken ct = default)
    {
        if (sourceConversationId == default)
        {
            return Task.FromResult(CreateRoutingFailureRouteResult(null, null, "Unknown source conversation"));
        }

        if (!_conversationStore.TryGet(sourceConversationId, out var sourceConversation))
        {
            return Task.FromResult(CreateRoutingFailureRouteResult(null, null, "Unknown source conversation"));
        }

        AgentPanelState? sourcePanel = null;
        if (sourceConversation.Kind == ConversationKind.Direct)
        {
            var peerActorId = ResolveDirectPeerActorId(sourceConversation);
            sourcePanel = _panelHost.GetOrCreatePanelForActor(peerActorId);
        }

        var sourceKey = sourcePanel?.PanelId ?? sourceConversationId.Value;
        return RouteAndExecuteCoreAsync(
            sourceConversationId,
            sourcePanel,
            sourceKey,
            rawInput,
            ct);
    }

    private async Task<RouteResult> RouteAndExecuteCoreAsync(
        ConversationId sourceConversationId,
        AgentPanelState? sourcePanel,
        string sourceKey,
        string rawInput,
        CancellationToken ct)
    {
        IReadOnlyList<string> rosterNames = _actorCatalog.ListAgents()
            .Select(static a => a.DisplayName)
            .ToList();

        var parseResult = _parser.Parse(sourceKey, rawInput, rosterNames);

        if (!parseResult.Success || parseResult.Intent is null)
        {
            return CreateRoutingFailureRouteResult(
                sourcePanel,
                sourceConversationId,
                parseResult.FailureReason ?? "Routing failed");
        }

        var intent = parseResult.Intent;
        if (sourcePanel is not null && sourcePanel.PanelId != intent.SourcePanelId)
        {
            return CreateRoutingFailureRouteResult(sourcePanel, sourceConversationId, "Unknown source panel");
        }

        if (!TryResolveTargetActor(intent, sourcePanel, out var targetActorId, out var resolveFailure))
        {
            return CreateRoutingFailureRouteResult(
                sourcePanel,
                sourceConversationId,
                resolveFailure ?? "Unknown target");
        }

        var targetPanel = intent.IsDirectSend
            ? sourcePanel ?? _panelHost.GetOrCreatePanelForActor(targetActorId)
            : _panelHost.GetOrCreatePanelForActor(targetActorId);

        var request = new RouteRequest(
            intent.SourcePanelId,
            targetPanel.ActorId,
            targetPanel.PanelId,
            targetPanel.ConversationId,
            intent.ContentAfterStrip,
            intent.IsDirectSend);

        var executionResult = await _coordinator.SendAsync(
            targetPanel.PanelId,
            request.ContentAfterStrip,
            ct);

        if (!intent.IsDirectSend && executionResult is not null)
        {
            ProjectSourceRouteStatus(
                sourceConversationId,
                sourcePanel,
                request,
                executionResult);
        }

        return new RouteResult(true, request, null, executionResult);
    }

    private void ProjectSourceRouteStatus(
        ConversationId sourceConversationId,
        AgentPanelState? sourcePanel,
        RouteRequest request,
        AgentExecutionCoordinatorResult executionResult)
    {
        if (!_conversationStore.TryGet(sourceConversationId, out _))
        {
            return;
        }

        var outcome = ResolveRouteOutcomeLabel(executionResult);
        var targetDisplayName = ResolveActorDisplayName(request.TargetActorId);
        var author = ResolveSourceRouteAuthor(sourceConversationId, sourcePanel);

        AgentConversationEventProjection.ProjectRouteStatus(
            _conversationStore,
            sourceConversationId,
            author,
            executionResult.Run.Id,
            request.TargetActorId,
            request.ConversationId,
            targetDisplayName,
            outcome);
    }

    private static string ResolveRouteOutcomeLabel(AgentExecutionCoordinatorResult executionResult) =>
        executionResult.Run.Outcome switch
        {
            ExecutionRunOutcome.Success => "Completed",
            ExecutionRunOutcome.Rejected => "Rejected",
            ExecutionRunOutcome.Cancelled => "Cancelled",
            ExecutionRunOutcome.RoutingFailure => "RoutingFailed",
            ExecutionRunOutcome.ExecutionFailure => "Failed",
            _ => "Failed",
        };

    private ActorId ResolveSourceRouteAuthor(ConversationId sourceConversationId, AgentPanelState? sourcePanel)
    {
        if (sourcePanel is not null)
        {
            return sourcePanel.ActorId;
        }

        if (_conversationStore.TryGet(sourceConversationId, out var conversation)
            && conversation.Kind == ConversationKind.Channel)
        {
            return _actorCatalog.CanonicalHuman.Id;
        }

        return _actorCatalog.CanonicalTownhallAgent.Id;
    }

    private string ResolveActorDisplayName(ActorId actorId)
    {
        if (_actorCatalog.TryGet(actorId, out var actor) && !string.IsNullOrWhiteSpace(actor.DisplayName))
        {
            return actor.DisplayName;
        }

        return actorId.Value;
    }

    private ActorId ResolveDirectPeerActorId(Conversation conversation)
    {
        var humanId = _actorCatalog.CanonicalHuman.Id;
        var peer = conversation.Participants.All.FirstOrDefault(participant => participant != humanId);
        if (peer == default)
        {
            throw new InvalidOperationException(
                $"Direct conversation '{conversation.Id.Value}' has no non-human participant.");
        }

        return peer;
    }

    private bool TryResolveTargetActor(
        ParsedRouteIntent intent,
        AgentPanelState? sourcePanel,
        out ActorId targetActorId,
        out string? failureReason)
    {
        if (intent.IsDirectSend)
        {
            if (sourcePanel is null)
            {
                targetActorId = default;
                failureReason = "Unknown source panel";
                return false;
            }

            targetActorId = sourcePanel.ActorId;
            failureReason = null;
            return true;
        }

        var matchedName = intent.MatchedAgentName;
        if (string.IsNullOrEmpty(matchedName))
        {
            targetActorId = default;
            failureReason = "Unknown target";
            return false;
        }

        var matches = _actorCatalog.ListAgents()
            .Where(a => string.Equals(a.DisplayName, matchedName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            targetActorId = default;
            failureReason = "Unknown target";
            return false;
        }

        if (matches.Count > 1)
        {
            targetActorId = default;
            failureReason = "Ambiguous target";
            return false;
        }

        targetActorId = matches[0].Id;
        failureReason = null;
        return true;
    }

    private RouteResult CreateRoutingFailureRouteResult(
        AgentPanelState? sourcePanel,
        ConversationId? sourceConversationId,
        string failureReason)
    {
        var executionResult = TryCreateAndRecordRoutingFailure(
            sourcePanel,
            sourceConversationId,
            failureReason);
        return new RouteResult(false, null, failureReason, executionResult);
    }

    private AgentExecutionCoordinatorResult? TryCreateAndRecordRoutingFailure(
        AgentPanelState? sourcePanel,
        ConversationId? sourceConversationId,
        string failureReason)
    {
        ConversationId? conversationId = sourceConversationId ?? sourcePanel?.ConversationId;
        if (conversationId is not { } resolvedConversationId)
        {
            return null;
        }

        var author = sourcePanel?.ActorId
            ?? (_conversationStore.TryGet(resolvedConversationId, out var conversation)
                && conversation.Kind == ConversationKind.Channel
                ? _actorCatalog.CanonicalHuman.Id
                : _actorCatalog.CanonicalTownhallAgent.Id);

        var runId = ExecutionRunId.New();
        var run = new ExecutionRun(
            runId,
            resolvedConversationId,
            ActorId.HumanUser,
            author,
            sourcePanel?.PanelId ?? resolvedConversationId.Value,
            ExecutionRunOutcome.RoutingFailure);

        if (_conversationStore.TryGet(resolvedConversationId, out _))
        {
            AgentConversationEventProjection.ProjectRoutingFailure(
                _conversationStore,
                resolvedConversationId,
                author,
                runId,
                failureReason);
        }

        return AgentExecutionCoordinatorResult.RoutingFailure(run, failureReason);
    }
}
