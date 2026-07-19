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
            "Assistant: hello");

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
    [InlineData(TownhallMessageKind.Chat, "user-1", ConversationEntryKind.UserChat)]
    [InlineData(TownhallMessageKind.Chat, "alpha", ConversationEntryKind.AssistantResponse)]
    [InlineData(TownhallMessageKind.AgentError, "alpha", ConversationEntryKind.RoutingFailure, "Routing failed: x")]
    [InlineData(TownhallMessageKind.AgentError, "alpha", ConversationEntryKind.ExecutionFailure, "Error: boom")]
    [InlineData(TownhallMessageKind.ChannelEvent, "user-1", ConversationEntryKind.ChannelEvent, "Switched")]
    [InlineData(TownhallMessageKind.System, "user-1", ConversationEntryKind.SystemNotification, "Status")]
    public void ClassifyTownhallMirror_MapsAuthorizedProducers(
        TownhallMessageKind kind,
        string legacySenderId,
        ConversationEntryKind expectedKind,
        string content = "hello")
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var author = legacySenderId == "user-1"
            ? ActorId.HumanUser
            : ActorId.PanelSeed("alpha");

        var classified = TownhallEntryProjection.ClassifyTownhallMirror(
            kind,
            author,
            content,
            catalog);

        Assert.Equal(expectedKind, classified);
    }

    [Fact]
    public void ClassifyTownhallMirror_RejectsUnusedSpeculativeKinds()
    {
        var catalog = ConversationsTestSupport.CreateCatalog();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TownhallEntryProjection.ClassifyTownhallMirror(
                TownhallMessageKind.AgentThink,
                ActorId.HumanUser,
                "thought",
                catalog));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TownhallEntryProjection.ClassifyTownhallMirror(
                TownhallMessageKind.ToolCall,
                ActorId.HumanUser,
                "tool",
                catalog));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TownhallEntryProjection.ClassifyTownhallMirror(
                TownhallMessageKind.ToolResult,
                ActorId.HumanUser,
                "result",
                catalog));
    }
}
