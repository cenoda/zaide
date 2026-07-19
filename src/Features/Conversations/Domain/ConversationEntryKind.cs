namespace Zaide.Features.Conversations.Domain;

/// <summary>
/// Authoritative classification for currently produced conversation entries.
/// Presentation kinds such as <c>AgentThink</c>, <c>ToolCall</c>, and
/// <c>ToolResult</c> are intentionally excluded.
/// </summary>
public enum ConversationEntryKind
{
    UserChat,
    AssistantResponse,
    RoutingFailure,
    ExecutionFailure,
    ChannelEvent,
    SystemNotification
}
