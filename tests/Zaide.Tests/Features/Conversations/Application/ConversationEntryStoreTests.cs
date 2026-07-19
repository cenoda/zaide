using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Conversations.Application;

public sealed class ConversationEntryStoreTests
{
    private static readonly DateTimeOffset Timestamp = DateTimeOffset.UtcNow;

    [Fact]
    public void AppendEntry_PreservesInsertionOrder()
    {
        var store = new ConversationStore();
        var conversation = store.CreateChannelConversation("channel-1");

        var first = ConversationEntry.UserChat(
            ConversationEntryId.New(),
            ActorId.HumanUser,
            Timestamp,
            "first");
        var second = ConversationEntry.AssistantResponse(
            ConversationEntryId.New(),
            ActorId.TownhallAgent,
            Timestamp.AddMinutes(1),
            "Assistant: second");

        store.AppendEntry(conversation.Id, first);
        store.AppendEntry(conversation.Id, second);

        Assert.True(store.TryGet(conversation.Id, out var updated));
        Assert.Equal(2, updated!.Entries.Count);
        Assert.Same(first, updated.Entries[0]);
        Assert.Same(second, updated.Entries[1]);
    }

    [Fact]
    public void AppendEntry_TargetsRequestedConversationId()
    {
        var store = new ConversationStore();
        var first = store.CreateChannelConversation("channel-1");
        var second = store.CreateChannelConversation("channel-2");
        var entry = ConversationEntry.UserChat(
            ConversationEntryId.New(),
            ActorId.HumanUser,
            Timestamp,
            "only on channel-2");

        store.AppendEntry(second.Id, entry);

        Assert.Empty(first.Entries);
        Assert.Single(second.Entries);
        Assert.Same(entry, second.Entries[0]);
    }

    [Fact]
    public void AppendEntry_UnknownConversation_ThrowsWithoutPartialMutation()
    {
        var store = new ConversationStore();
        var conversation = store.CreateChannelConversation("channel-1");
        var unknownId = ConversationId.ForChannel("missing");
        var entry = ConversationEntry.UserChat(
            ConversationEntryId.New(),
            ActorId.HumanUser,
            Timestamp,
            "orphan");

        var ex = Assert.Throws<KeyNotFoundException>(() => store.AppendEntry(unknownId, entry));
        Assert.Contains("missing", ex.Message, StringComparison.Ordinal);
        Assert.Empty(conversation.Entries);
    }

    [Fact]
    public void Entries_ExposeImmutableOrderedView()
    {
        var store = new ConversationStore();
        var conversation = store.CreateChannelConversation("channel-1");
        store.AppendEntry(
            conversation.Id,
            ConversationEntry.UserChat(
                ConversationEntryId.New(),
                ActorId.HumanUser,
                Timestamp,
                "one"));

        var snapshot = conversation.Entries;
        var list = (IList)snapshot;
        Assert.Throws<NotSupportedException>(() => list.Add(
            ConversationEntry.UserChat(
                ConversationEntryId.New(),
                ActorId.HumanUser,
                Timestamp,
                "blocked")));
        Assert.Single(conversation.Entries);
    }

    [Fact]
    public void AppendEntry_RejectsNullEntry()
    {
        var store = new ConversationStore();
        var conversation = store.CreateChannelConversation("channel-1");

        Assert.Throws<ArgumentNullException>(() => store.AppendEntry(conversation.Id, null!));
        Assert.Empty(conversation.Entries);
    }
}
