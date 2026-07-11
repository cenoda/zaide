using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Models;
using Zaide.Services;

namespace Zaide.ViewModels;

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

    public async Task SendAsync(string panelId, string userMessage, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(panelId))
            return;

        if (string.IsNullOrWhiteSpace(userMessage))
            return;

        // --- Resolve panel ---
        var panel = _panelHost.Panels.FirstOrDefault(p => p.PanelId == panelId);
        if (panel is null)
            return;

        // --- One-in-flight enforcement ---
        if (!_inFlightPanels.Add(panelId))
            return; // Already in flight for this panel

        // --- Mark busy / Thinking (also resets Error state on new send) ---
        panel.Status = "Thinking";
        panel.IsBusy = true;

        try
        {
            // Append user message to output history
            panel.OutputHistory.Add($"User: {userMessage}");

            // Consume the draft immediately so the input box clears and the same
            // text cannot be re-sent by pressing Enter again (e.g. when the
            // request later fails). The user can always re-type to retry.
            panel.DraftInput = string.Empty;

            // Execute
            // NOTE: Do NOT use ConfigureAwait(false) here. The continuation
            // mutates AgentPanelState (OutputHistory, Status, IsBusy) which are
            // bound to Avalonia views and must be updated on the UI thread.
            // AgentExecutionService internally uses ConfigureAwait(false) for
            // its own I/O (that is correct), but after it returns we need the
            // captured SynchronizationContext so the callers — including
            // MainWindowViewModel.SendAgentMessageAsync — also remain on the
            // Avalonia UI thread for Townhall mirroring.
            var result = await _executionService.ExecuteAsync(userMessage, ct);

            if (result.IsSuccess)
            {
                panel.OutputHistory.Add($"Assistant: {result.ResponseText}");
                panel.Status = "Idle";
            }
            else
            {
                panel.OutputHistory.Add($"Error: {result.ErrorMessage}");
                panel.Status = "Error";
            }
        }
        catch (Exception ex)
        {
            panel.OutputHistory.Add($"Error: {ex.Message}");
            panel.Status = "Error";
        }
        finally
        {
            panel.IsBusy = false;
            _inFlightPanels.Remove(panelId);
        }
    }
}