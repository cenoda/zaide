using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;
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
        ConversationId? mirrorTargetConversationId = null;
        if (_townhallViewModel.TryGetActiveChannelConversationId(out var capturedConversationId))
        {
            mirrorTargetConversationId = capturedConversationId;
            _townhallViewModel.AddMirroredActivityToConversation(
                capturedConversationId,
                entryKind: ConversationEntryKind.UserChat,
                content: userMessage,
                author: _actorCatalog.CanonicalHuman.Id,
                senderId: _actorCatalog.CanonicalHuman.ProjectedLegacyId,
                senderName: _actorCatalog.CanonicalHuman.DisplayName);
        }

        var routeResult = await _agentRouter.RouteAndExecuteAsync(panelId, userMessage, ct);

        if (routeResult.ExecutionResult is not null && mirrorTargetConversationId is not null)
        {
            MirrorTerminalExecution(mirrorTargetConversationId.Value, routeResult.ExecutionResult);
        }
    }

    private void MirrorTerminalExecution(
        ConversationId mirrorTargetConversationId,
        AgentExecutionCoordinatorResult executionResult)
    {
        var run = executionResult.Run;
        var panel = _agentPanelHost.Panels.FirstOrDefault(p => p.PanelId == run.TargetPanelId);

        string senderId;
        string senderName;
        if (panel is not null)
        {
            senderId = panel.AgentId;
            senderName = panel.AgentName;
        }
        else if (_actorCatalog.TryGet(run.TargetActorId, out var actor))
        {
            senderId = actor.ProjectedLegacyId;
            senderName = actor.DisplayName;
        }
        else
        {
            return;
        }

        switch (run.Outcome)
        {
            case ExecutionRunOutcome.Success:
                _townhallViewModel.AddMirroredActivityToConversation(
                    mirrorTargetConversationId,
                    entryKind: ConversationEntryKind.AssistantResponse,
                    content: executionResult.AssistantResponse!,
                    author: run.TargetActorId,
                    senderId: senderId,
                    senderName: senderName);
                break;

            case ExecutionRunOutcome.RoutingFailure:
                _townhallViewModel.AddMirroredActivityToConversation(
                    mirrorTargetConversationId,
                    entryKind: ConversationEntryKind.RoutingFailure,
                    content: executionResult.ErrorMessage!,
                    author: run.TargetActorId,
                    senderId: senderId,
                    senderName: senderName);
                break;

            case ExecutionRunOutcome.ExecutionFailure:
            case ExecutionRunOutcome.Cancelled:
                _townhallViewModel.AddMirroredActivityToConversation(
                    mirrorTargetConversationId,
                    entryKind: ConversationEntryKind.ExecutionFailure,
                    content: executionResult.ErrorMessage!,
                    author: run.TargetActorId,
                    senderId: senderId,
                    senderName: senderName);
                break;
        }
    }
}
