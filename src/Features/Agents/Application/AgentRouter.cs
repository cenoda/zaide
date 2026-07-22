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

    public async Task<RouteResult> RouteAndExecuteAsync(
        string sourcePanelId,
        string rawInput,
        CancellationToken ct = default)
    {
        var sourcePanel = _panelHost.Panels.FirstOrDefault(p => p.PanelId == sourcePanelId);

        IReadOnlyList<string> rosterNames = _actorCatalog.ListAgents()
            .Select(static a => a.DisplayName)
            .ToList();

        var parseResult = _parser.Parse(sourcePanelId, rawInput, rosterNames);

        if (!parseResult.Success || parseResult.Intent is null)
        {
            return CreateRoutingFailureRouteResult(
                sourcePanel,
                parseResult.FailureReason ?? "Routing failed");
        }

        var intent = parseResult.Intent;
        if (sourcePanel is null || sourcePanel.PanelId != intent.SourcePanelId)
        {
            return CreateRoutingFailureRouteResult(sourcePanel, "Unknown source panel");
        }

        if (!TryResolveTargetActor(intent, sourcePanel, out var targetActorId, out var resolveFailure))
        {
            return CreateRoutingFailureRouteResult(
                sourcePanel,
                resolveFailure ?? "Unknown target");
        }

        var targetPanel = intent.IsDirectSend
            ? sourcePanel
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

        return new RouteResult(true, request, null, executionResult);
    }

    private bool TryResolveTargetActor(
        ParsedRouteIntent intent,
        AgentPanelState sourcePanel,
        out ActorId targetActorId,
        out string? failureReason)
    {
        if (intent.IsDirectSend)
        {
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
        string failureReason)
    {
        var executionResult = TryCreateAndRecordRoutingFailure(sourcePanel, failureReason);
        return new RouteResult(false, null, failureReason, executionResult);
    }

    private AgentExecutionCoordinatorResult? TryCreateAndRecordRoutingFailure(
        AgentPanelState? sourcePanel,
        string failureReason)
    {
        if (sourcePanel is null)
            return null;

        var runId = ExecutionRunId.New();
        var run = new ExecutionRun(
            runId,
            sourcePanel.ConversationId,
            ActorId.HumanUser,
            sourcePanel.ActorId,
            sourcePanel.PanelId,
            ExecutionRunOutcome.RoutingFailure);

        // Record failure on the owning conversation when the store holds it
        // (shared production wiring). Skip silently when a test double uses a
        // detached store — still return a structured routing-failure result.
        if (_conversationStore.TryGet(sourcePanel.ConversationId, out _))
        {
            AgentConversationEventProjection.ProjectRoutingFailure(
                _conversationStore,
                sourcePanel.ConversationId,
                sourcePanel.ActorId,
                runId,
                failureReason);
        }

        return AgentExecutionCoordinatorResult.RoutingFailure(run, failureReason);
    }
}
