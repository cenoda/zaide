using System;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Pure compatibility projection from authoritative typed direct-conversation
/// entries to the existing Agent Panel <c>OutputHistory</c> string protocol.
/// </summary>
internal static class AgentPanelEntryProjection
{
    public static string ToOutputHistoryLine(ConversationEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return entry.Kind switch
        {
            ConversationEntryKind.UserChat => $"User: {entry.Content}",
            ConversationEntryKind.AssistantResponse => $"Assistant: {entry.Content}",
            ConversationEntryKind.ExecutionFailure => $"Error: {entry.Content}",
            ConversationEntryKind.RoutingFailure =>
                $"Routing failed: {entry.Content}",
            _ => throw new ArgumentOutOfRangeException(
                nameof(entry),
                entry.Kind,
                "Unsupported Agent Panel output projection.")
        };
    }
}
