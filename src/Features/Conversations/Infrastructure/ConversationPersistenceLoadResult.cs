namespace Zaide.Features.Conversations.Infrastructure;

/// <summary>
/// Outcome of loading the conversation workspace snapshot from disk.
/// </summary>
internal enum ConversationPersistenceLoadResult
{
    Missing,
    Loaded,
    Corrupt,
    UnsupportedVersion
}
