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
/// Orchestrates agent send flow by composing <see cref="IAgentPanelHost"/> and
/// <see cref="IAgentExecutionService"/>. Owns per-<see cref="ConversationId"/>
/// one-in-flight enforcement; panel chrome is a thin projection of conversation
/// busy/status/draft. No View, Townhall, or provider-platform references.
/// </summary>
public sealed class AgentExecutionCoordinator : IAgentExecutionCoordinator
{
    private readonly IAgentPanelHost _panelHost;
    private readonly IAgentExecutionService _executionService;
    private readonly IConversationStore _conversationStore;
    private readonly IConversationDraftState? _draftState;
    private readonly HashSet<ConversationId> _inFlightConversations = new();
    private readonly object _sync = new();

    public AgentExecutionCoordinator(
        IAgentPanelHost panelHost,
        IAgentExecutionService executionService,
        IConversationStore conversationStore,
        IConversationDraftState? draftState = null)
    {
        _panelHost = panelHost ?? throw new ArgumentNullException(nameof(panelHost));
        _executionService = executionService ?? throw new ArgumentNullException(nameof(executionService));
        _conversationStore = conversationStore ?? throw new ArgumentNullException(nameof(conversationStore));
        _draftState = draftState;
    }

    public event Action<ConversationId, bool>? ConversationBusyChanged;

    public bool IsConversationBusy(ConversationId conversationId)
    {
        lock (_sync)
        {
            return _inFlightConversations.Contains(conversationId);
        }
    }

    public async Task<AgentExecutionCoordinatorResult?> SendAsync(
        string panelId,
        string userMessage,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(panelId))
            return null;

        if (string.IsNullOrWhiteSpace(userMessage))
            return null;

        var panel = _panelHost.Panels.FirstOrDefault(p => p.PanelId == panelId);
        if (panel is null)
            return null;

        var conversationId = panel.ConversationId;
        if (!TryBeginInFlight(conversationId))
            return null;

        var runId = ExecutionRunId.New();
        ApplyPanelBusyProjection(conversationId, isBusy: true, status: "Thinking");

        ExecutionRunOutcome outcome;
        string? assistantResponse = null;
        string? errorMessage = null;

        try
        {
            AgentPanelDirectConversationWriter.AppendUserMessage(
                _conversationStore,
                panel,
                runId,
                userMessage);
            ClearDraft(panel);

            var result = await _executionService.ExecuteAsync(userMessage, ct);

            if (result.IsSuccess)
            {
                var responseText = result.ResponseText;
                if (string.IsNullOrWhiteSpace(responseText))
                {
                    errorMessage = "Assistant response was empty.";
                    AgentPanelDirectConversationWriter.AppendExecutionFailure(
                        _conversationStore,
                        panel,
                        runId,
                        errorMessage);
                    ApplyPanelBusyProjection(conversationId, isBusy: false, status: "Error");
                    outcome = ExecutionRunOutcome.ExecutionFailure;
                }
                else
                {
                    assistantResponse = responseText;
                    AgentPanelDirectConversationWriter.AppendAssistantResponse(
                        _conversationStore,
                        panel,
                        runId,
                        assistantResponse);
                    ApplyPanelBusyProjection(conversationId, isBusy: false, status: "Idle");
                    outcome = ExecutionRunOutcome.Success;
                }
            }
            else
            {
                errorMessage = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "Request failed."
                    : result.ErrorMessage;
                AgentPanelDirectConversationWriter.AppendExecutionFailure(
                    _conversationStore,
                    panel,
                    runId,
                    errorMessage);
                ApplyPanelBusyProjection(conversationId, isBusy: false, status: "Error");
                outcome = IsCancellationMessage(errorMessage)
                    ? ExecutionRunOutcome.Cancelled
                    : ExecutionRunOutcome.ExecutionFailure;
            }
        }
        catch (OperationCanceledException ex)
        {
            errorMessage = string.IsNullOrWhiteSpace(ex.Message)
                ? "The operation was canceled."
                : ex.Message;
            AgentPanelDirectConversationWriter.AppendExecutionFailure(
                _conversationStore,
                panel,
                runId,
                errorMessage);
            ApplyPanelBusyProjection(conversationId, isBusy: false, status: "Error");
            outcome = ExecutionRunOutcome.Cancelled;
        }
        catch (Exception ex)
        {
            errorMessage = string.IsNullOrWhiteSpace(ex.Message)
                ? "Request failed."
                : ex.Message;
            AgentPanelDirectConversationWriter.AppendExecutionFailure(
                _conversationStore,
                panel,
                runId,
                errorMessage);
            ApplyPanelBusyProjection(conversationId, isBusy: false, status: "Error");
            outcome = ExecutionRunOutcome.ExecutionFailure;
        }
        finally
        {
            EndInFlight(conversationId);
            // Ensure busy projection is cleared even if a status path was missed.
            var stillBusy = false;
            lock (_sync)
            {
                stillBusy = _inFlightConversations.Contains(conversationId);
            }

            if (!stillBusy)
            {
                var livePanel = FindPanelForConversation(conversationId);
                if (livePanel is not null && livePanel.IsBusy)
                {
                    livePanel.IsBusy = false;
                }
            }
        }

        // Re-resolve panel for result correlation — original panel may have been closed.
        var resultPanelId = FindPanelForConversation(conversationId)?.PanelId ?? panel.PanelId;
        var targetActorId = panel.ActorId;

        var run = new ExecutionRun(
            runId,
            conversationId,
            ActorId.HumanUser,
            targetActorId,
            resultPanelId,
            outcome);

        return outcome switch
        {
            ExecutionRunOutcome.Success => AgentExecutionCoordinatorResult.Success(
                run,
                assistantResponse!),
            ExecutionRunOutcome.Cancelled or ExecutionRunOutcome.ExecutionFailure =>
                AgentExecutionCoordinatorResult.Failure(run, errorMessage!),
            _ => throw new InvalidOperationException(
                $"Unexpected coordinator outcome: {outcome}.")
        };
    }

    private bool TryBeginInFlight(ConversationId conversationId)
    {
        lock (_sync)
        {
            if (!_inFlightConversations.Add(conversationId))
            {
                return false;
            }
        }

        ConversationBusyChanged?.Invoke(conversationId, true);
        return true;
    }

    private void EndInFlight(ConversationId conversationId)
    {
        lock (_sync)
        {
            _inFlightConversations.Remove(conversationId);
        }

        ConversationBusyChanged?.Invoke(conversationId, false);
    }

    private void ApplyPanelBusyProjection(ConversationId conversationId, bool isBusy, string status)
    {
        var livePanel = FindPanelForConversation(conversationId);
        if (livePanel is null)
        {
            return;
        }

        livePanel.Status = status;
        livePanel.IsBusy = isBusy;
    }

    private AgentPanelState? FindPanelForConversation(ConversationId conversationId) =>
        _panelHost.Panels.FirstOrDefault(p => p.ConversationId == conversationId);

    private void ClearDraft(AgentPanelState panel)
    {
        panel.DraftInput = string.Empty;
        _draftState?.ClearDraft(panel.ConversationId);
    }

    private static bool IsCancellationMessage(string? message) =>
        message is not null
        && message.Contains("cancelled", StringComparison.OrdinalIgnoreCase);
}
