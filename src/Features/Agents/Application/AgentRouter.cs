using System;
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
            // NOTE: Do NOT use ConfigureAwait(false) here — the coordinator's
            // continuation mutates AgentPanelState on the caller's
            // SynchronizationContext (Avalonia UI thread). The coordinator
            // also no longer uses ConfigureAwait(false) internally.
            await _coordinator.SendAsync(request.SourcePanelId, request.ContentAfterStrip, ct);
        }
        else
        {
            // M4: first real routed agent-to-agent flow. Resolve target panel by AgentName.
            var targetPanel = _panelHost.Panels.FirstOrDefault(p => p.AgentName == request.TargetAgentName);
            var targetPanelId = targetPanel?.PanelId ?? request.SourcePanelId;
            // NOTE: Do NOT use ConfigureAwait(false) here — same UI-thread
            // reason as the direct-send path above.
            await _coordinator.SendAsync(targetPanelId, request.ContentAfterStrip, ct);
        }

        return result;
    }
}
