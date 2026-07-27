using System;
using System.Collections.Generic;
using System.Linq;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Filtering and budgeting rules for prior-conversation replay. Replay is read-only
/// and never persisted by the harness.
/// </summary>
internal sealed class NativeHarnessPriorConversationReplayPolicy
{
    public static IReadOnlySet<ConversationEntryKind> DefaultIncludedKinds { get; } =
        new HashSet<ConversationEntryKind>
        {
            ConversationEntryKind.UserChat,
            ConversationEntryKind.AssistantResponse,
        };

    public static IReadOnlySet<ConversationEntryKind> DefaultExcludedKinds { get; } =
        new HashSet<ConversationEntryKind>
        {
            ConversationEntryKind.RoutingFailure,
            ConversationEntryKind.ExecutionFailure,
            ConversationEntryKind.ChannelEvent,
            ConversationEntryKind.SystemNotification,
        };

    public NativeHarnessPriorConversationReplayPolicy(
        int maxTokenBudget,
        int maxEntryCount,
        IReadOnlySet<ConversationEntryKind>? includedKinds = null)
    {
        if (maxTokenBudget < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxTokenBudget),
                maxTokenBudget,
                "Max token budget must be positive.");
        }

        if (maxEntryCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxEntryCount),
                maxEntryCount,
                "Max entry count must be positive.");
        }

        var kinds = includedKinds ?? DefaultIncludedKinds;
        if (kinds.Count == 0)
        {
            throw new ArgumentException("At least one included kind is required.", nameof(includedKinds));
        }

        if (kinds.Any(kind => DefaultExcludedKinds.Contains(kind)))
        {
            throw new ArgumentException(
                "Included kinds cannot overlap default excluded kinds.",
                nameof(includedKinds));
        }

        MaxTokenBudget = maxTokenBudget;
        MaxEntryCount = maxEntryCount;
        IncludedKinds = kinds;
    }

    public int MaxTokenBudget { get; }

    public int MaxEntryCount { get; }

    public IReadOnlySet<ConversationEntryKind> IncludedKinds { get; }

    public static NativeHarnessPriorConversationReplayPolicy CreateStandard() =>
        new(maxTokenBudget: 4_000, maxEntryCount: 50);
}
