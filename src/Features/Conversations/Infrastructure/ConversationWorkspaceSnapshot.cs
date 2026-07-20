using System;
using System.Collections.Generic;

namespace Zaide.Features.Conversations.Infrastructure;

/// <summary>
/// Versioned on-disk conversation workspace snapshot (schema v1).
/// Conversations feature owns persistence; Townhall UI maps are embedded here.
/// </summary>
internal sealed class ConversationWorkspaceSnapshot
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public List<PersistedChannelSnapshot> Channels { get; set; } = new();

    public List<PersistedConversationSnapshot> Conversations { get; set; } = new();

    public string? ActiveConversationId { get; set; }

    public Dictionary<string, string> Drafts { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> LastReadEntryIds { get; set; } = new(StringComparer.Ordinal);
}
