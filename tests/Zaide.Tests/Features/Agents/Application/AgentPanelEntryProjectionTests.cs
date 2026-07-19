using System;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents.Application;

public sealed class AgentPanelEntryProjectionTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(ConversationEntryKind.UserChat, "hello", "User: hello")]
    [InlineData(ConversationEntryKind.AssistantResponse, "reply", "Assistant: reply")]
    [InlineData(ConversationEntryKind.ExecutionFailure, "boom", "Error: boom")]
    public void ToOutputHistoryLine_PreservesExactLegacyPrefixes(
        ConversationEntryKind kind,
        string content,
        string expected)
    {
        var entry = CreateEntry(kind, content);

        var line = AgentPanelEntryProjection.ToOutputHistoryLine(entry);

        Assert.Equal(expected, line);
    }

    [Theory]
    [InlineData(ConversationEntryKind.RoutingFailure)]
    [InlineData(ConversationEntryKind.ChannelEvent)]
    [InlineData(ConversationEntryKind.SystemNotification)]
    public void TryToOutputHistoryLine_UnsupportedPanelKinds_ReturnFalse(ConversationEntryKind kind)
    {
        var entry = CreateEntry(kind, "payload");

        Assert.False(AgentPanelEntryProjection.TryToOutputHistoryLine(entry, out _));
    }

    [Fact]
    public void ToOutputHistoryLine_UnsupportedKind_Throws()
    {
        var entry = ConversationEntry.ChannelEvent(
            ConversationEntryId.New(),
            ActorId.HumanUser,
            Timestamp,
            "joined");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => AgentPanelEntryProjection.ToOutputHistoryLine(entry));
    }

    private static ConversationEntry CreateEntry(ConversationEntryKind kind, string content) =>
        kind switch
        {
            ConversationEntryKind.UserChat =>
                ConversationEntry.UserChat(
                    ConversationEntryId.New(),
                    ActorId.HumanUser,
                    Timestamp,
                    content),
            ConversationEntryKind.AssistantResponse =>
                ConversationEntry.AssistantResponse(
                    ConversationEntryId.New(),
                    ActorId.PanelSeed("alpha"),
                    Timestamp,
                    content),
            ConversationEntryKind.ExecutionFailure =>
                ConversationEntry.ExecutionFailure(
                    ConversationEntryId.New(),
                    ActorId.PanelSeed("alpha"),
                    Timestamp,
                    content),
            ConversationEntryKind.RoutingFailure =>
                ConversationEntry.RoutingFailure(
                    ConversationEntryId.New(),
                    ActorId.PanelSeed("alpha"),
                    Timestamp,
                    content),
            ConversationEntryKind.ChannelEvent =>
                ConversationEntry.ChannelEvent(
                    ConversationEntryId.New(),
                    ActorId.HumanUser,
                    Timestamp,
                    content),
            ConversationEntryKind.SystemNotification =>
                ConversationEntry.SystemNotification(
                    ConversationEntryId.New(),
                    ActorId.HumanUser,
                    Timestamp,
                    content),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
}
