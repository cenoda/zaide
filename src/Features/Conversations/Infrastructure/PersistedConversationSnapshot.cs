using System;
using System.Collections.Generic;

namespace Zaide.Features.Conversations.Infrastructure;

/// <summary>
/// Durable conversation aggregate row (schema v1).
/// </summary>
internal sealed class PersistedConversationSnapshot
{
    public string Id { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public List<string> Participants { get; set; } = new();

    public List<PersistedConversationEntrySnapshot> Entries { get; set; } = new();
}
