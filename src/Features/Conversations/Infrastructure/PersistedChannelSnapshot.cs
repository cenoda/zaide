namespace Zaide.Features.Conversations.Infrastructure;

/// <summary>
/// Durable Townhall channel navigation row (schema v1).
/// </summary>
internal sealed class PersistedChannelSnapshot
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool Pinned { get; set; }
}
