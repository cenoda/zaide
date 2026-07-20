using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts;

namespace Zaide.App.Shell;

/// <summary>
/// Owns the agent-panel send flow (route and execute). Constructed inside
/// <see cref="MainWindowViewModel"/>; not DI-registered.
/// </summary>
internal sealed class AgentTownhallMirrorCoordinator
{
    private readonly IAgentRouter _agentRouter;

    public AgentTownhallMirrorCoordinator(IAgentRouter agentRouter)
    {
        _agentRouter = agentRouter;
    }

    /// <summary>
    /// Routes an agent message and executes on the resolved target panel.
    /// Direct conversation entries remain authoritative on the panel path.
    /// </summary>
    public async Task SendAsync(string panelId, string userMessage, CancellationToken ct) =>
        await _agentRouter.RouteAndExecuteAsync(panelId, userMessage, ct);
}
