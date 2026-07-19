using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// M4 implementation of the narrow routing orchestration seam.
/// Composes MentionParser + IAgentPanelHost (for resolution) + IAgentExecutionCoordinator.
/// Resolves typed target identity once after unchanged visible-name parsing.
/// Keeps direct-send behavior intact. No provider widening.
/// </summary>
public sealed class AgentRouter : IAgentRouter
{
    private readonly MentionParser _parser;
    private readonly IAgentPanelHost _panelHost;
    private readonly IAgentExecutionCoordinator _coordinator;

    public AgentRouter(MentionParser parser, IAgentPanelHost panelHost, IAgentExecutionCoordinator coordinator)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _panelHost = panelHost ?? throw new ArgumentNullException(nameof(panelHost));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public async Task<RouteResult> RouteAndExecuteAsync(
        string sourcePanelId,
        string rawInput,
        CancellationToken ct = default)
    {
        var sourcePanel = _panelHost.Panels.FirstOrDefault(p => p.PanelId == sourcePanelId);

        IReadOnlyList<string> visibleAgentNames = _panelHost.Panels
            .Select(static p => p.AgentName)
            .ToList();

        var parseResult = _parser.Parse(sourcePanelId, rawInput, visibleAgentNames);

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

        var targetPanel = ResolveTargetPanel(intent, sourcePanel);
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

    private static RouteResult CreateRoutingFailureRouteResult(
        AgentPanelState? sourcePanel,
        string failureReason)
    {
        var executionResult = TryCreateRoutingFailureResult(sourcePanel, failureReason);
        return new RouteResult(false, null, failureReason, executionResult);
    }

    private static AgentExecutionCoordinatorResult? TryCreateRoutingFailureResult(
        AgentPanelState? sourcePanel,
        string failureReason)
    {
        if (sourcePanel is null)
            return null;

        var run = new ExecutionRun(
            ExecutionRunId.New(),
            sourcePanel.ConversationId,
            ActorId.HumanUser,
            sourcePanel.ActorId,
            sourcePanel.PanelId,
            ExecutionRunOutcome.RoutingFailure);

        return AgentExecutionCoordinatorResult.RoutingFailure(run, failureReason);
    }

    private AgentPanelState ResolveTargetPanel(ParsedRouteIntent intent, AgentPanelState sourcePanel)
    {
        if (intent.IsDirectSend)
            return sourcePanel;

        var targetPanel = _panelHost.Panels.FirstOrDefault(
            p => string.Equals(p.AgentName, intent.MatchedAgentName, StringComparison.OrdinalIgnoreCase));

        return targetPanel ?? sourcePanel;
    }
}
