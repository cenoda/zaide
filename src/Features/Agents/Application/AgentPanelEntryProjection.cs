using System;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Pure compatibility projection from authoritative typed direct-conversation
/// entries to the existing Agent Panel output string protocol.
/// </summary>
internal static class AgentPanelEntryProjection
{
    public static bool TryToOutputHistoryLine(ConversationEntry entry, out string line)
    {
        ArgumentNullException.ThrowIfNull(entry);

        switch (entry.Kind)
        {
            case ConversationEntryKind.UserChat:
                line = $"User: {entry.Content}";
                return true;
            case ConversationEntryKind.AssistantResponse:
                line = $"Assistant: {entry.Content}";
                return true;
            case ConversationEntryKind.ExecutionFailure:
                line = $"Error: {entry.Content}";
                return true;
            case ConversationEntryKind.RoutingFailure:
                line = $"Error: {entry.Content}";
                return true;
            default:
                line = string.Empty;
                return false;
        }
    }

    public static string ToOutputHistoryLine(ConversationEntry entry)
    {
        if (!TryToOutputHistoryLine(entry, out var line))
        {
            throw new ArgumentOutOfRangeException(
                nameof(entry),
                entry.Kind,
                "Unsupported Agent Panel output projection.");
        }

        return line;
    }
}
