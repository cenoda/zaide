using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Narrow Phase 6 M3 routing orchestration seam.
/// Owns mention parsing, source/target resolution, direct-vs-routed decision,
/// and coordination of execution. Returns outcome for truthful app-layer flow.
/// Tiny and specific to M3 routing orchestration only.
/// </summary>
public interface IAgentRouter
{
    Task<RouteResult> RouteAndExecuteAsync(string sourcePanelId, string rawInput, CancellationToken ct = default);

    /// <summary>
    /// Routes from a typed source conversation (direct or channel) without
    /// fabricating a panel identity. Channel plain chat must not call this path.
    /// </summary>
    Task<RouteResult> RouteAndExecuteFromConversationAsync(
        ConversationId sourceConversationId,
        string rawInput,
        CancellationToken ct = default);
}
