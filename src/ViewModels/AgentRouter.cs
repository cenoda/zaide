using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Models;
using Zaide.Services;
using Zaide.ViewModels;

namespace Zaide.ViewModels;
/// <summary>
/// M4 implementation of the narrow routing orchestration seam.
/// Composes MentionParser + IAgentPanelHost (for resolution) + IAgentExecutionCoordinator.
/// Implements first real routed flow to target panel when mention present.
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

    public async Task<RouteResult> RouteAndExecuteAsync(string sourcePanelId, string rawInput, CancellationToken ct = default)
    {
        var result = _parser.Parse(sourcePanelId, rawInput);

        if (!result.Success || result.Request is null)
        {
            // Failure cases (unknown/ambiguous/multi/empty) surface without direct-send fallback
            return result;
        }

        var request = result.Request;

        if (request.IsDirectSend)
        {
            // Preserve existing direct-send runtime behavior
            await _coordinator.SendAsync(request.SourcePanelId, request.ContentAfterStrip, ct).ConfigureAwait(false);
        }
        else
        {
            // M4: first real routed agent-to-agent flow. Resolve target panel by AgentName.
            var targetPanel = _panelHost.Panels.FirstOrDefault(p => p.AgentName == request.TargetAgentName);
            var targetPanelId = targetPanel?.PanelId ?? request.SourcePanelId;
            await _coordinator.SendAsync(targetPanelId, request.ContentAfterStrip, ct).ConfigureAwait(false);
        }

        return result;
    }
}
