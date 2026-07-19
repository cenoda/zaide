using System;

namespace Zaide.Features.Conversations.Domain;

/// <summary>
/// Immutable typed conversation entry for Refactor 7 M3. Construct only
/// through the kind-specific factory methods on this type.
/// </summary>
public sealed class ConversationEntry
{
    private ConversationEntry(
        ConversationEntryId id,
        ConversationEntryKind kind,
        ActorId author,
        DateTimeOffset timestamp,
        string content,
        ConversationEntryCorrelationId? correlationId)
    {
        Id = id;
        Kind = kind;
        Author = author;
        Timestamp = timestamp;
        Content = content;
        CorrelationId = correlationId;
    }

    public ConversationEntryId Id { get; }

    public ConversationEntryKind Kind { get; }

    public ActorId Author { get; }

    public DateTimeOffset Timestamp { get; }

    public string Content { get; }

    /// <summary>
    /// Optional producer-supplied correlation token for entries that belong to
    /// one bounded execution attempt.
    /// </summary>
    public ConversationEntryCorrelationId? CorrelationId { get; }

    public static ConversationEntry UserChat(
        ConversationEntryId id,
        ActorId author,
        DateTimeOffset timestamp,
        string content,
        ConversationEntryCorrelationId? correlationId = null) =>
        Create(id, ConversationEntryKind.UserChat, author, timestamp, content, correlationId);

    public static ConversationEntry AssistantResponse(
        ConversationEntryId id,
        ActorId author,
        DateTimeOffset timestamp,
        string content,
        ConversationEntryCorrelationId? correlationId = null) =>
        Create(id, ConversationEntryKind.AssistantResponse, author, timestamp, content, correlationId);

    public static ConversationEntry RoutingFailure(
        ConversationEntryId id,
        ActorId author,
        DateTimeOffset timestamp,
        string content,
        ConversationEntryCorrelationId? correlationId = null) =>
        Create(id, ConversationEntryKind.RoutingFailure, author, timestamp, content, correlationId);

    public static ConversationEntry ExecutionFailure(
        ConversationEntryId id,
        ActorId author,
        DateTimeOffset timestamp,
        string content,
        ConversationEntryCorrelationId? correlationId = null) =>
        Create(id, ConversationEntryKind.ExecutionFailure, author, timestamp, content, correlationId);

    public static ConversationEntry ChannelEvent(
        ConversationEntryId id,
        ActorId author,
        DateTimeOffset timestamp,
        string content,
        ConversationEntryCorrelationId? correlationId = null) =>
        Create(id, ConversationEntryKind.ChannelEvent, author, timestamp, content, correlationId);

    public static ConversationEntry SystemNotification(
        ConversationEntryId id,
        ActorId author,
        DateTimeOffset timestamp,
        string content,
        ConversationEntryCorrelationId? correlationId = null) =>
        Create(id, ConversationEntryKind.SystemNotification, author, timestamp, content, correlationId);

    private static ConversationEntry Create(
        ConversationEntryId id,
        ConversationEntryKind kind,
        ActorId author,
        DateTimeOffset timestamp,
        string content,
        ConversationEntryCorrelationId? correlationId)
    {
        if (id == default)
        {
            throw new ArgumentException("Entry id is required.", nameof(id));
        }

        if (author == default)
        {
            throw new ArgumentException("Entry author is required.", nameof(author));
        }

        if (timestamp == default)
        {
            throw new ArgumentException("Entry timestamp is required.", nameof(timestamp));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Entry content is required.", nameof(content));
        }

        if (correlationId is { } presentCorrelation
            && string.IsNullOrWhiteSpace(presentCorrelation.Value))
        {
            throw new ArgumentException(
                "Correlation id must be omitted or contain a non-empty value.",
                nameof(correlationId));
        }

        return new ConversationEntry(id, kind, author, timestamp, content, correlationId);
    }
}
