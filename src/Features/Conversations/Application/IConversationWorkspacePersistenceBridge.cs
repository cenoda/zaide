using System;
using System.Collections.Generic;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Conversations.Infrastructure;

namespace Zaide.Features.Conversations.Application;

/// <summary>
/// Townhall-owned presentation state captured alongside conversation aggregates.
/// Implemented by Townhall; Conversations persistence calls it for load/save.
/// </summary>
internal interface IConversationWorkspacePersistenceBridge
{
    /// <summary>Raised when presentation-owned durable fields change.</summary>
    event Action? PresentationStateChanged;

    /// <summary>Whether a snapshot was applied during startup recovery.</summary>
    bool WasRestoredFromPersistence { get; }

    /// <summary>Last active conversation id from disk when restored.</summary>
    string? RestoredActiveConversationId { get; }

    void NotifyPresentationStateChanged();

    void ApplyRestoredSnapshot(ConversationWorkspaceSnapshot snapshot);

    ConversationWorkspaceSnapshot CapturePresentationSnapshot(
        IReadOnlyList<Conversation> conversations);
}
