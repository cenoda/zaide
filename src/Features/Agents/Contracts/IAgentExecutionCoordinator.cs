using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Orchestration seam for panel send flow.
/// Composes <see cref="IAgentPanelHost"/> and <see cref="IAgentExecutionService"/>
/// to update panel-visible state. No View, Townhall, or provider-platform references.
/// </summary>
public interface IAgentExecutionCoordinator
{
    /// <summary>
    /// Sends a user message from the specified panel to the configured execution
    /// service. Appends user/assistant text to the panel's output history and
    /// clears draft input on success. Enforces one in-flight request per panel.
    /// </summary>
    /// <param name="panelId">The panel to send from.</param>
    /// <param name="userMessage">The user message text.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the send operation finishes.</returns>
    Task SendAsync(string panelId, string userMessage, CancellationToken ct = default);
}