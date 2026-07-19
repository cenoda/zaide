using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;

namespace Zaide.App.Shell;

/// <summary>
/// Owns the agent-panel send flow and Townhall mirror side-effects.
/// Constructed inside <see cref="MainWindowViewModel"/>; not DI-registered.
/// </summary>
internal sealed class AgentTownhallMirrorCoordinator
{
    private readonly IAgentRouter _agentRouter;
    private readonly IAgentPanelHost _agentPanelHost;
    private readonly TownhallViewModel _townhallViewModel;
    private readonly IActorCatalog _actorCatalog;

    public AgentTownhallMirrorCoordinator(
        IAgentRouter agentRouter,
        IAgentPanelHost agentPanelHost,
        TownhallViewModel townhallViewModel,
        IActorCatalog actorCatalog)
    {
        _agentRouter = agentRouter;
        _agentPanelHost = agentPanelHost;
        _townhallViewModel = townhallViewModel;
        _actorCatalog = actorCatalog ?? throw new ArgumentNullException(nameof(actorCatalog));
    }

    /// <summary>
    /// Routes an agent message and mirrors user/response/error activity into Townhall.
    /// Preserves pre-extraction behavior of
    /// <see cref="MainWindowViewModel.SendAgentMessageAsync"/>.
    /// </summary>
    public async Task SendAsync(string panelId, string userMessage, CancellationToken ct)
    {
        // Mirror the user request into Townhall before routing (preserves current truthful behavior).
        _townhallViewModel.AddMirroredActivity(
            kind: TownhallMessageKind.Chat,
            content: userMessage,
            author: _actorCatalog.CanonicalHuman.Id,
            senderId: _actorCatalog.CanonicalHuman.ProjectedLegacyId,
            senderName: _actorCatalog.CanonicalHuman.DisplayName);

        // Delegate entirely to the routing orchestration seam (M3).
        // NOTE: Do NOT use ConfigureAwait(false) here. The continuation reads
        // AgentPanelState (OutputHistory, Status) and calls
        // TownhallViewModel.AddMirroredActivity() which modifies
        // ObservableCollection<TownhallMessage> — both require the Avalonia UI
        // thread. AgentRouter and AgentExecutionCoordinator also preserve the
        // captured SynchronizationContext internally.
        var routeResult = await _agentRouter.RouteAndExecuteAsync(panelId, userMessage, ct);

        // M1: consume the routing outcome so routed flows and routing failures
        // become visible in Townhall (previously the result was captured but unread).
        var sourcePanel = _agentPanelHost.Panels.FirstOrDefault(p => p.PanelId == panelId);

        // Case A: parse/routing failure. Surface as an AgentError under the source
        // panel identity. If the source panel is gone, there is nothing to attribute to.
        if (!routeResult.Success)
        {
            if (sourcePanel is null)
                return;

            _townhallViewModel.AddMirroredActivity(
                kind: TownhallMessageKind.AgentError,
                content: $"Routing failed: {routeResult.FailureReason}",
                author: sourcePanel.ActorId,
                senderId: sourcePanel.AgentId,
                senderName: sourcePanel.AgentName);
            return;
        }

        // Choose which panel's output to mirror:
        //   Case B (routed): the resolved target panel.
        //   Case C (direct send): the source panel (unchanged existing behavior).
        var request = routeResult.Request;
        var panel = request is not null && !request.IsDirectSend
            ? _agentPanelHost.Panels.FirstOrDefault(p => p.AgentName == request.TargetAgentName)
            : sourcePanel;

        if (panel is null)
            return;

        if (panel.Status == "Error")
        {
            var lastOutput = panel.OutputHistory.Count > 0 ? panel.OutputHistory[^1] : null;
            if (lastOutput is not null && lastOutput.StartsWith("Error: "))
            {
                _townhallViewModel.AddMirroredActivity(
                    kind: TownhallMessageKind.AgentError,
                    content: lastOutput,
                    author: panel.ActorId,
                    senderId: panel.AgentId,
                    senderName: panel.AgentName);
            }
        }
        else
        {
            var lastOutput = panel.OutputHistory.Count > 0
                ? panel.OutputHistory[^1]
                : null;
            if (lastOutput is not null && lastOutput.StartsWith("Assistant: "))
            {
                _townhallViewModel.AddMirroredActivity(
                    kind: TownhallMessageKind.Chat,
                    content: lastOutput,
                    author: panel.ActorId,
                    senderId: panel.AgentId,
                    senderName: panel.AgentName);
            }
        }
    }
}
