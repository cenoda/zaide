using System;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Orchestration seam for agent send flow.
/// Composes panel thin-host projection and <see cref="IAgentExecutionService"/>
/// while owning conversation-keyed in-flight state. No View, Townhall, or
/// provider-platform references.
/// </summary>
public interface IAgentExecutionCoordinator
{
    /// <summary>
    /// Raised when a conversation's in-flight busy flag changes.
    /// Arguments: conversation id, is-busy.
    /// </summary>
    event Action<ConversationId, bool>? ConversationBusyChanged;

    /// <summary>
    /// True when the given conversation currently has an admitted in-flight send.
    /// Survives panel close and navigation; not tied to panel chrome lifetime.
    /// </summary>
    bool IsConversationBusy(ConversationId conversationId);

    /// <summary>
    /// Sends a user message for the panel's owning conversation. Appends
    /// user/assistant/failure entries to that conversation, clears the shared
    /// draft, and enforces one in-flight request per conversation.
    /// </summary>
    /// <param name="panelId">Thin-host panel id used to resolve the conversation.</param>
    /// <param name="userMessage">The user message text.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Structured execution result when an attempt is admitted; otherwise
    /// <see langword="null"/> for no-op paths.
    /// </returns>
    Task<AgentExecutionCoordinatorResult?> SendAsync(
        string panelId,
        string userMessage,
        CancellationToken ct = default);
}
