using System;
using Xunit;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;
using Zaide.Tests.Features.Conversations;

namespace Zaide.Tests.Features.Townhall.Presentation;

public sealed class TownhallEntryProjectionTests
{
    private static readonly DateTimeOffset Timestamp = DateTimeOffset.Parse("2026-07-19T12:00:00Z");

    [Theory]
    [InlineData(ConversationEntryKind.UserChat, TownhallMessageKind.Chat)]
    [InlineData(ConversationEntryKind.AssistantResponse, TownhallMessageKind.Chat)]
    [InlineData(ConversationEntryKind.RoutingFailure, TownhallMessageKind.AgentError)]
    [InlineData(ConversationEntryKind.ExecutionFailure, TownhallMessageKind.AgentError)]
    [InlineData(ConversationEntryKind.ChannelEvent, TownhallMessageKind.ChannelEvent)]
    [InlineData(ConversationEntryKind.SystemNotification, TownhallMessageKind.System)]
    public void ToTownhallMessageKind_MapsExactCompatibilityValues(
        ConversationEntryKind entryKind,
        TownhallMessageKind expectedKind)
    {
        Assert.Equal(expectedKind, TownhallEntryProjection.ToTownhallMessageKind(entryKind));
    }

    [Fact]
    public void ToTownhallMessage_ProjectsExactSenderContentPrefixAvatarAndTimestamp()
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var entry = ConversationEntry.AssistantResponse(
            ConversationEntryId.New(),
            ActorId.PanelSeed("alpha"),
            Timestamp,
            "hello");

        var message = TownhallEntryProjection.ToTownhallMessage(entry, catalog);

        Assert.Equal(entry.Id.Value, message.Id);
        Assert.Equal("alpha", message.SenderId);
        Assert.Equal("Alpha", message.SenderName);
        Assert.Equal("avatar-agent", message.SenderAvatar);
        Assert.Equal("Assistant: hello", message.Content);
        Assert.Equal(Timestamp, message.Timestamp);
        Assert.Equal(TownhallMessageKind.Chat, message.Kind);
    }

    [Fact]
    public void ToTownhallMessage_UsesHumanAvatarForCanonicalUser()
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var entry = ConversationEntry.UserChat(
            ConversationEntryId.New(),
            ActorId.HumanUser,
            Timestamp,
            "hello");

        var message = TownhallEntryProjection.ToTownhallMessage(entry, catalog);

        Assert.Equal("user-1", message.SenderId);
        Assert.Equal("User", message.SenderName);
        Assert.Equal("avatar-user", message.SenderAvatar);
    }

    [Fact]
    public void ToTownhallMessage_FallsBackToLegacyOverridesForUnknownAuthors()
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var entry = ConversationEntry.AssistantResponse(
            ConversationEntryId.New(),
            ActorId.PanelCustom("agent-5"),
            Timestamp,
            "Agent message");

        var message = TownhallEntryProjection.ToTownhallMessage(
            entry,
            catalog,
            projectedLegacySenderId: "agent-5",
            projectedSenderName: "Some Agent");

        Assert.Equal("agent-5", message.SenderId);
        Assert.Equal("Some Agent", message.SenderName);
        Assert.Equal("avatar-agent", message.SenderAvatar);
    }

    [Theory]
    [InlineData(ConversationEntryKind.UserChat, "hello", "hello")]
    [InlineData(ConversationEntryKind.AssistantResponse, "reply", "Assistant: reply")]
    [InlineData(ConversationEntryKind.RoutingFailure, "unknown", "Routing failed: unknown")]
    [InlineData(ConversationEntryKind.ExecutionFailure, "boom", "Error: boom")]
    [InlineData(ConversationEntryKind.ChannelEvent, "Switched", "Switched")]
    [InlineData(ConversationEntryKind.SystemNotification, "Status", "Status")]
    public void ToTownhallDisplayContent_PreservesFrozenCompatibilityPrefixes(
        ConversationEntryKind entryKind,
        string rawContent,
        string expectedDisplay)
    {
        var entry = TownhallEntryProjection.CreateTypedEntry(
            entryKind,
            ActorId.HumanUser,
            Timestamp,
            rawContent);

        Assert.Equal(expectedDisplay, TownhallEntryProjection.ToTownhallDisplayContent(entry));
    }
}
