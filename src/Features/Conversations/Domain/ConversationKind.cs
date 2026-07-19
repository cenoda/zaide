namespace Zaide.Features.Conversations.Domain;

/// <summary>
/// Authoritative conversation classification. Panel association is presentation
/// state and is not a conversation kind.
/// </summary>
public enum ConversationKind
{
    Channel,
    Direct,
}
