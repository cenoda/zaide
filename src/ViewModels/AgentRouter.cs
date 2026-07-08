using System;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Models;
using Zaide.Services;

namespace Zaide.ViewModels;

/// <summary>
/// M3 implementation of the narrow routing orchestration seam.
/// Composes MentionParser + IAgentPanelHost (for resolution) + IAgentExecutionCoordinator.
/// Keeps direct-send behavior via delegation for no-mention case.
/// Does not own Townhall policy or execution transport.
/// </summary>
public sealed class AgentRouter : IAgentRouter
{
    private readonly MentionParser _parser;
    private readonly IAgentExecutionCoordinator _coordinator;

    public AgentRouter(MentionParser parser, IAgentExecutionCoordinator coordinator)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
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
            // M3: routed intent resolved but execution delegation for direct only per scope lock.
            // Routed execution path not expanded in M3.
            await _coordinator.SendAsync(request.SourcePanelId, request.ContentAfterStrip, ct).ConfigureAwait(false);
        }

        return result;
    }
}
