using System;
using System.Reflection;
using Xunit;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Conversations.Domain;

public sealed class ConversationEntryTests
{
    private static readonly DateTimeOffset Timestamp = DateTimeOffset.UtcNow;

    [Theory]
    [InlineData(nameof(ConversationEntry.UserChat), ConversationEntryKind.UserChat)]
    [InlineData(nameof(ConversationEntry.AssistantResponse), ConversationEntryKind.AssistantResponse)]
    [InlineData(nameof(ConversationEntry.RoutingFailure), ConversationEntryKind.RoutingFailure)]
    [InlineData(nameof(ConversationEntry.ExecutionFailure), ConversationEntryKind.ExecutionFailure)]
    [InlineData(nameof(ConversationEntry.ChannelEvent), ConversationEntryKind.ChannelEvent)]
    [InlineData(nameof(ConversationEntry.SystemNotification), ConversationEntryKind.SystemNotification)]
    public void FactoryMethods_CreateAuthorizedClassifications(
        string factoryName,
        ConversationEntryKind expectedKind)
    {
        var entry = InvokeFactory(factoryName);

        Assert.Equal(expectedKind, entry.Kind);
        Assert.Equal(ActorId.HumanUser, entry.Author);
        Assert.Equal("hello", entry.Content);
        Assert.Equal(Timestamp, entry.Timestamp);
        Assert.NotEqual(default, entry.Id);
        Assert.Null(entry.CorrelationId);
    }

    [Theory]
    [InlineData(nameof(ConversationEntry.UserChat))]
    [InlineData(nameof(ConversationEntry.AssistantResponse))]
    [InlineData(nameof(ConversationEntry.RoutingFailure))]
    [InlineData(nameof(ConversationEntry.ExecutionFailure))]
    [InlineData(nameof(ConversationEntry.ChannelEvent))]
    [InlineData(nameof(ConversationEntry.SystemNotification))]
    public void FactoryMethods_AllowOmittedCorrelation(string factoryName)
    {
        var entry = InvokeFactory(factoryName);

        Assert.Null(entry.CorrelationId);
    }

    [Theory]
    [InlineData(nameof(ConversationEntry.UserChat))]
    [InlineData(nameof(ConversationEntry.AssistantResponse))]
    [InlineData(nameof(ConversationEntry.RoutingFailure))]
    [InlineData(nameof(ConversationEntry.ExecutionFailure))]
    [InlineData(nameof(ConversationEntry.ChannelEvent))]
    [InlineData(nameof(ConversationEntry.SystemNotification))]
    public void FactoryMethods_RetainValidCorrelation(string factoryName)
    {
        var correlation = ConversationEntryCorrelationId.FromValue("run:correlated");

        var entry = InvokeFactory(factoryName, correlation);

        Assert.Equal(correlation, entry.CorrelationId);
    }

    [Theory]
    [InlineData(nameof(ConversationEntry.UserChat))]
    [InlineData(nameof(ConversationEntry.AssistantResponse))]
    [InlineData(nameof(ConversationEntry.RoutingFailure))]
    [InlineData(nameof(ConversationEntry.ExecutionFailure))]
    [InlineData(nameof(ConversationEntry.ChannelEvent))]
    [InlineData(nameof(ConversationEntry.SystemNotification))]
    public void FactoryMethods_RejectPresentButDefaultCorrelation(string factoryName)
    {
        Assert.Throws<ArgumentException>(() =>
            InvokeFactory(factoryName, default(ConversationEntryCorrelationId)));
    }

    [Fact]
    public void Factory_RejectsDefaultEntryId()
    {
        Assert.Throws<ArgumentException>(() =>
            ConversationEntry.UserChat(default, ActorId.HumanUser, Timestamp, "hello"));
    }

    [Fact]
    public void Factory_RejectsDefaultAuthor()
    {
        Assert.Throws<ArgumentException>(() =>
            ConversationEntry.UserChat(ConversationEntryId.New(), default, Timestamp, "hello"));
    }

    [Fact]
    public void Factory_RejectsDefaultTimestamp()
    {
        Assert.Throws<ArgumentException>(() =>
            ConversationEntry.UserChat(ConversationEntryId.New(), ActorId.HumanUser, default, "hello"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Factory_RejectsMissingContent(string? content)
    {
        Assert.Throws<ArgumentException>(() =>
            ConversationEntry.UserChat(
                ConversationEntryId.New(),
                ActorId.HumanUser,
                Timestamp,
                content!));
    }

    [Fact]
    public void PublicSurface_HasNoUserConstructibleConstructor()
    {
        var constructors = typeof(ConversationEntry).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance);
        Assert.Empty(constructors);
    }

    private static ConversationEntry InvokeFactory(
        string factoryName,
        ConversationEntryCorrelationId? correlationId = null)
    {
        var method = typeof(ConversationEntry).GetMethod(
            factoryName,
            BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);

        try
        {
            return (ConversationEntry)method!.Invoke(
                null,
                new object?[]
                {
                    ConversationEntryId.New(),
                    ActorId.HumanUser,
                    Timestamp,
                    "hello",
                    correlationId
                })!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }
}
