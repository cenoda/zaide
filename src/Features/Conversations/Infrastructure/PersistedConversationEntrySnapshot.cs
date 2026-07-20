using System;

namespace Zaide.Features.Conversations.Infrastructure;

/// <summary>
/// Durable conversation entry row (schema v1).
/// </summary>
internal sealed class PersistedConversationEntrySnapshot
{
    public string Id { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; }

    public string Content { get; set; } = string.Empty;

    public string? CorrelationId { get; set; }
}
