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
/// Orchestrates panel send flow by composing <see cref="IAgentPanelHost"/> and
/// <see cref="IAgentExecutionService"/>. Owns per-panel one-in-flight enforcement,
/// output history updates, and draft clearing. No View, Townhall, or
/// provider-platform references.
/// </summary>
public sealed class AgentExecutionCoordinator : IAgentExecutionCoordinator
{
    private readonly IAgentPanelHost _panelHost;
    private readonly IAgentExecutionService _executionService;
    private readonly HashSet<string> _inFlightPanels = new();

    public AgentExecutionCoordinator(IAgentPanelHost panelHost, IAgentExecutionService executionService)
    {
        _panelHost = panelHost ?? throw new ArgumentNullException(nameof(panelHost));
        _executionService = executionService ?? throw new ArgumentNullException(nameof(executionService));
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

        if (!_inFlightPanels.Add(panelId))
            return null;

        var runId = ExecutionRunId.New();

        panel.Status = "Thinking";
        panel.IsBusy = true;

        ExecutionRunOutcome outcome;
        string? assistantResponse = null;
        string? errorMessage = null;

        try
        {
            panel.OutputHistory.Add($"User: {userMessage}");
            panel.DraftInput = string.Empty;

            var result = await _executionService.ExecuteAsync(userMessage, ct);

            if (result.IsSuccess)
            {
                assistantResponse = result.ResponseText;
                panel.OutputHistory.Add($"Assistant: {assistantResponse}");
                panel.Status = "Idle";
                outcome = ExecutionRunOutcome.Success;
            }
            else
            {
                errorMessage = result.ErrorMessage;
                panel.OutputHistory.Add($"Error: {errorMessage}");
                panel.Status = "Error";
                outcome = IsCancellationMessage(errorMessage)
                    ? ExecutionRunOutcome.Cancelled
                    : ExecutionRunOutcome.ExecutionFailure;
            }
        }
        catch (OperationCanceledException ex)
        {
            errorMessage = ex.Message;
            panel.OutputHistory.Add($"Error: {errorMessage}");
            panel.Status = "Error";
            outcome = ExecutionRunOutcome.Cancelled;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            panel.OutputHistory.Add($"Error: {errorMessage}");
            panel.Status = "Error";
            outcome = ExecutionRunOutcome.ExecutionFailure;
        }
        finally
        {
            panel.IsBusy = false;
            _inFlightPanels.Remove(panelId);
        }

        var run = new ExecutionRun(
            runId,
            panel.ConversationId,
            ActorId.HumanUser,
            panel.ActorId,
            panel.PanelId,
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

    private static bool IsCancellationMessage(string? message) =>
        message is not null
        && message.Contains("cancelled", StringComparison.OrdinalIgnoreCase);
}
