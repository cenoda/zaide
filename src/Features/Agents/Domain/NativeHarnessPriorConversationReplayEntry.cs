using System;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// One prior conversation entry selected for bounded replay into model context.
/// Distinct from <see cref="ConversationEntry"/> and in-run loop history records.
/// </summary>
internal sealed class NativeHarnessPriorConversationReplayEntry
{
    public NativeHarnessPriorConversationReplayEntry(
        ConversationEntryId entryId,
        ConversationEntryKind kind,
        ActorId author,
        string text,
        int estimatedTokenCount)
    {
        if (entryId == default)
        {
            throw new ArgumentException("Entry id is required.", nameof(entryId));
        }

        if (author == default)
        {
            throw new ArgumentException("Author is required.", nameof(author));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Replay text is required.", nameof(text));
        }

        if (estimatedTokenCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(estimatedTokenCount),
                estimatedTokenCount,
                "Estimated token count cannot be negative.");
        }

        if (kind is not ConversationEntryKind.UserChat
            and not ConversationEntryKind.AssistantResponse)
        {
            throw new ArgumentException(
                "Only user chat and assistant response entries may be replayed.",
                nameof(kind));
        }

        EntryId = entryId;
        Kind = kind;
        Author = author;
        Text = text;
        EstimatedTokenCount = estimatedTokenCount;
    }

    public ConversationEntryId EntryId { get; }

    public ConversationEntryKind Kind { get; }

    public ActorId Author { get; }

    public string Text { get; }

    public int EstimatedTokenCount { get; }
}
