using System;
using System.Collections.Generic;
using Xunit;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents.Contracts;

public sealed class Phase19ContractsPriorConversationTests
{
    [Fact]
    public void Phase19Contracts_ReplayPolicy_ExcludesNonChatKindsByDefault()
    {
        var policy = NativeHarnessPriorConversationReplayPolicy.CreateStandard();

        Assert.Contains(ConversationEntryKind.UserChat, policy.IncludedKinds);
        Assert.Contains(ConversationEntryKind.AssistantResponse, policy.IncludedKinds);
        Assert.DoesNotContain(ConversationEntryKind.RoutingFailure, policy.IncludedKinds);
        Assert.DoesNotContain(ConversationEntryKind.ExecutionFailure, policy.IncludedKinds);
    }

    [Fact]
    public void Phase19Contracts_ReplayEntry_RejectsRoutingFailureKind()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new NativeHarnessPriorConversationReplayEntry(
                ConversationEntryId.FromValue("entry:1"),
                ConversationEntryKind.RoutingFailure,
                ActorId.FromValue("actor:user"),
                "failed",
                estimatedTokenCount: 1));

        Assert.Equal("kind", exception.ParamName);
    }

    [Fact]
    public void Phase19Contracts_ReplayRequest_RequiresCurrentMessageEntryId()
    {
        var policy = NativeHarnessPriorConversationReplayPolicy.CreateStandard();

        var exception = Assert.Throws<ArgumentException>(() =>
            new NativeHarnessPriorConversationReplayRequest(
                ConversationId.FromValue("conversation:1"),
                default,
                policy));

        Assert.Equal("currentMessageEntryId", exception.ParamName);
    }

    [Fact]
    public void Phase19Contracts_ReplayPolicy_RejectsOverlappingIncludedAndExcludedKinds()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new NativeHarnessPriorConversationReplayPolicy(
                maxTokenBudget: 100,
                maxEntryCount: 10,
                includedKinds: new HashSet<ConversationEntryKind>
                {
                    ConversationEntryKind.UserChat,
                    ConversationEntryKind.RoutingFailure,
                }));

        Assert.Equal("includedKinds", exception.ParamName);
    }
}
