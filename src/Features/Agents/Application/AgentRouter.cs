using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Presentation;

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
        IReadOnlyList<string> visibleAgentNames = _panelHost.Panels
            .Select(static p => p.AgentName)
            .ToList();

        var parseResult = _parser.Parse(sourcePanelId, rawInput, visibleAgentNames);

        if (!parseResult.Success || parseResult.Intent is null)
        {
            return new RouteResult(false, null, parseResult.FailureReason, null);
        }

        var intent = parseResult.Intent;
        var sourcePanel = _panelHost.Panels.FirstOrDefault(p => p.PanelId == intent.SourcePanelId);
        if (sourcePanel is null)
        {
            return new RouteResult(false, null, "Unknown source panel", null);
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

    private AgentPanelState ResolveTargetPanel(ParsedRouteIntent intent, AgentPanelState sourcePanel)
    {
        if (intent.IsDirectSend)
            return sourcePanel;

        var targetPanel = _panelHost.Panels.FirstOrDefault(
            p => string.Equals(p.AgentName, intent.MatchedAgentName, StringComparison.OrdinalIgnoreCase));

        return targetPanel ?? sourcePanel;
    }
}
