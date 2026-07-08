using System;
using System.Collections.Generic;

namespace Zaide.Models;

/// <summary>
/// Represents a single entry in the Townhall activity log: a chat message,
/// a channel event, an agent action/thought, a tool call/result, an agent
/// error, or a system notification. All entry kinds share this model;
/// <see cref="Kind"/> distinguishes the type.
/// </summary>
public class TownhallMessage
{
    /// <summary>
    /// Unique identifier for this entry.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// ID of the sender (agent or user).
    /// </summary>
    public string SenderId { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the sender.
    /// </summary>
    public string SenderName { get; set; } = string.Empty;

    /// <summary>
    /// Avatar path or resource key for the sender.
    /// </summary>
    public string SenderAvatar { get; set; } = string.Empty;

    /// <summary>
    /// Content text of the entry.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the entry was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// The kind of entry this is (Chat, ChannelEvent, AgentAction, etc.).
    /// </summary>
    public TownhallMessageKind Kind { get; set; } = TownhallMessageKind.Chat;

    /// <summary>
    /// Free-text provider identifier, e.g. "openai", "anthropic", "local".
    /// Nullable; not populated by anything in Phase 4. Reserved for Phase 5/6
    /// agent execution.
    /// </summary>
    public string? SourceProvider { get; set; }

    /// <summary>
    /// Free-text model identifier, e.g. "gpt-4", "claude-3.5-sonnet".
    /// Nullable; not populated by anything in Phase 4. Reserved for Phase 5/6
    /// agent execution.
    /// </summary>
    public string? SourceModel { get; set; }

    /// <summary>
    /// Flat grouping id for a conversation turn. Nullable; not populated by
    /// anything in Phase 4. No ReplyToId / branching — that is explicitly
    /// deferred to Phase 6.
    /// </summary>
    public string? ThreadId { get; set; }

    /// <summary>
    /// Provider-specific extras (token counts, raw tool-call ids, etc.)
    /// that don't warrant a first-class field. Nullable.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Defines the kinds of entries that can appear in the Townhall activity log.
///
/// <list type="bullet">
///   <item><see cref="Chat"/> — A user or agent chat message.</item>
///   <item><see cref="ChannelEvent"/> — A channel lifecycle event (join, leave, rename, etc.).</item>
///   <item><see cref="AgentAction"/> — An agent performing a visible action (e.g. editing a file, running a command).</item>
///   <item><see cref="AgentThink"/> — An agent's internal reasoning step (streamed thought tokens). No producer in Phase 4 — included so the enum doesn't need a breaking change when agent execution lands.</item>
///   <item><see cref="ToolCall"/> — An agent's request to invoke a tool/function. No producer in Phase 4.</item>
///   <item><see cref="ToolResult"/> — The result returned by a tool/function. No producer in Phase 4.</item>
///   <item><see cref="AgentError"/> — An error originating from an agent (timeout, API error, invalid response). No producer in Phase 4.</item>
///   <item><see cref="System"/> — A system-generated notification (status changes, warnings, etc.).</item>
/// </list>
///
/// <see cref="AgentThink"/>, <see cref="ToolCall"/>, <see cref="ToolResult"/>,
/// and <see cref="AgentError"/> have no producer in Phase 4. They are defined
/// now as schema insurance so that adding agent execution in Phase 5/6 does
/// not require a breaking change to this enum.
/// </summary>
public enum TownhallMessageKind
{
    /// <summary>
    /// A user or agent chat message.
    /// </summary>
    Chat,

    /// <summary>
    /// A channel lifecycle event (join, leave, rename, etc.).
    /// </summary>
    ChannelEvent,

    /// <summary>
    /// An agent performing a visible action (editing a file, running a command, etc.).
    /// </summary>
    AgentAction,

    /// <summary>
    /// An agent's internal reasoning step (streamed thought tokens).
    /// No producer in Phase 4 — schema insurance for Phase 5/6.
    /// </summary>
    AgentThink,

    /// <summary>
    /// An agent's request to invoke a tool/function.
    /// No producer in Phase 4 — schema insurance for Phase 5/6.
    /// </summary>
    ToolCall,

    /// <summary>
    /// The result returned by a tool/function.
    /// No producer in Phase 4 — schema insurance for Phase 5/6.
    /// </summary>
    ToolResult,

    /// <summary>
    /// An error originating from an agent (timeout, API error, invalid response).
    /// No producer in Phase 4 — schema insurance for Phase 5/6.
    /// </summary>
    AgentError,

    /// <summary>
    /// A system-generated notification (status changes, warnings, etc.).
    /// </summary>
    System
}

/// <summary>
/// Filter mode for the Townhall chat panel (M3).
/// Three states for the segmented toggle.
/// </summary>
public enum FilterMode
{
    /// <summary>
    /// Show all entries (chat + activity).
    /// </summary>
    All,

    /// <summary>
    /// Show only Chat kind entries.
    /// </summary>
    ChatOnly,

    /// <summary>
    /// Show only non-Chat (activity/log) entries.
    /// </summary>
    ActivityOnly
}
