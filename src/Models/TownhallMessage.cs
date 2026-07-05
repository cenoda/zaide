using System;

namespace Zaide.Models;

/// <summary>
/// Represents a message in the Townhall chat system.
/// </summary>
public class TownhallMessage
{
    /// <summary>
    /// Unique identifier for the message.
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
    /// Content text of the message.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the message was sent.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Type of message (Normal, Warning, or System).
    /// </summary>
    public TownhallMessageType Type { get; set; } = TownhallMessageType.Normal;
}

/// <summary>
/// Defines the types of messages in the Townhall chat system.
/// </summary>
public enum TownhallMessageType
{
    /// <summary>
    /// Normal user message.
    /// </summary>
    Normal,

    /// <summary>
    /// Warning message ( amber alert icon shown).
    /// </summary>
    Warning,

    /// <summary>
    /// System-generated message.
    /// </summary>
    System
}