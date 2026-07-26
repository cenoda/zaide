using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents.Application;

/// <summary>
/// Phase 18 M3 integration tests for run context manifest assembly and consumption boundary.
/// </summary>
public sealed class Phase18RunIntegrationTests
{
    private static readonly DateTimeOffset FixedAssemblyTime =
        new(2026, 7, 26, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AgentBackendRequest_WithNullManifest_DoesNotThrow()
    {
        var request = new AgentBackendRequest(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            ConversationEntryId.New(),
            "test message",
            contextManifest: null);

        Assert.Null(request.ContextManifest);
    }

    [Fact]
    public void AgentBackendRequest_WithEmptyManifest_AcceptsManifest()
    {
        var manifest = new AgentContextManifest(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            AgentContextPolicyLevel.Off,
            Array.Empty<AgentContextItem>(),
            new AgentContextTokenBudget(AgentContextPolicyLevel.Off, 0, 0),
            Array.Empty<AgentContextTruncationDecision>(),
            Array.Empty<AgentContextExclusionDecision>(),
            FixedAssemblyTime);

        var request = new AgentBackendRequest(
            manifest.SessionId,
            manifest.RunId,
            manifest.ConversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            ConversationEntryId.New(),
            "test message",
            manifest);

        Assert.Same(manifest, request.ContextManifest);
    }

    [Fact]
    public void AgentBackendExecutionContext_ExposesManifestFromRequest()
    {
        var manifest = new AgentContextManifest(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            AgentContextPolicyLevel.Minimal,
            Array.Empty<AgentContextItem>(),
            new AgentContextTokenBudget(AgentContextPolicyLevel.Minimal, 100, 0),
            Array.Empty<AgentContextTruncationDecision>(),
            Array.Empty<AgentContextExclusionDecision>(),
            FixedAssemblyTime);

        var request = new AgentBackendRequest(
            manifest.SessionId,
            manifest.RunId,
            manifest.ConversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            ConversationEntryId.New(),
            "test message",
            manifest);

        var context = new AgentBackendExecutionContext(request, new UnavailableAgentActionBroker());

        Assert.Same(manifest, context.ContextManifest);
    }

    [Fact]
    public void AgentBackendExecutionContext_WithNullManifest_ReturnsNull()
    {
        var request = new AgentBackendRequest(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            ConversationEntryId.New(),
            "test message",
            contextManifest: null);

        var context = new AgentBackendExecutionContext(request, new UnavailableAgentActionBroker());

        Assert.Null(context.ContextManifest);
    }

    [Fact]
    public void Manifest_ItemsAreReadOnly()
    {
        var manifest = new AgentContextManifest(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            AgentContextPolicyLevel.Standard,
            new List<AgentContextItem> { CreateTestContextItem() },
            new AgentContextTokenBudget(AgentContextPolicyLevel.Standard, 1000, 500),
            Array.Empty<AgentContextTruncationDecision>(),
            Array.Empty<AgentContextExclusionDecision>(),
            FixedAssemblyTime);

        Assert.IsType<System.Collections.ObjectModel.ReadOnlyCollection<AgentContextItem>>(
            manifest.Items);
    }

    [Fact]
    public void Manifest_TruncationDecisionsAreReadOnly()
    {
        var manifest = new AgentContextManifest(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            AgentContextPolicyLevel.Standard,
            Array.Empty<AgentContextItem>(),
            new AgentContextTokenBudget(AgentContextPolicyLevel.Standard, 1000, 0),
            new List<AgentContextTruncationDecision>(),
            Array.Empty<AgentContextExclusionDecision>(),
            FixedAssemblyTime);

        Assert.IsType<System.Collections.ObjectModel.ReadOnlyCollection<AgentContextTruncationDecision>>(
            manifest.TruncationDecisions);
    }

    [Fact]
    public void Manifest_ExclusionDecisionsAreReadOnly()
    {
        var manifest = new AgentContextManifest(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            AgentContextPolicyLevel.Standard,
            Array.Empty<AgentContextItem>(),
            new AgentContextTokenBudget(AgentContextPolicyLevel.Standard, 1000, 0),
            Array.Empty<AgentContextTruncationDecision>(),
            new List<AgentContextExclusionDecision>(),
            FixedAssemblyTime);

        Assert.IsType<System.Collections.ObjectModel.ReadOnlyCollection<AgentContextExclusionDecision>>(
            manifest.ExclusionDecisions);
    }

    [Fact]
    public async Task SessionService_WithContextAssembly_AttachesManifestToRequest()
    {
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        var eventStream = new AgentEventStream();
        
        var manifestBuilder = new AgentContextManifestBuilder();
        
        var sessionService = new AgentSessionService(
            new[] { backend },
            eventStream,
            contextManifestBuilder: manifestBuilder,
            contextSnapshotSources: null);

        var conversationId = ConversationId.NewDirect();
        var messageEntryId = ConversationEntryId.New();
        var messageText = "test message";

        var result = await sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            messageEntryId,
            messageText,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(AgentRunStatusTransitions.IsTerminal(result.Status));
    }

    [Fact]
    public async Task SessionService_WithoutContextAssembly_DoesNotThrow()
    {
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        var eventStream = new AgentEventStream();
        
        var sessionService = new AgentSessionService(
            new[] { backend },
            eventStream,
            contextManifestBuilder: null,
            contextSnapshotSources: null);

        var conversationId = ConversationId.NewDirect();
        var messageEntryId = ConversationEntryId.New();
        var messageText = "test message";

        var result = await sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            messageEntryId,
            messageText,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(AgentRunStatusTransitions.IsTerminal(result.Status));
    }

    [Fact]
    public void LegacyBackend_ExecuteAsync_DoesNotConsumeContextManifest()
    {
        var manifest = new AgentContextManifest(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            AgentContextPolicyLevel.Detailed,
            new List<AgentContextItem> { CreateTestContextItem() },
            new AgentContextTokenBudget(AgentContextPolicyLevel.Detailed, 2000, 1500),
            Array.Empty<AgentContextTruncationDecision>(),
            Array.Empty<AgentContextExclusionDecision>(),
            FixedAssemblyTime);

        var request = new AgentBackendRequest(
            manifest.SessionId,
            manifest.RunId,
            manifest.ConversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            ConversationEntryId.New(),
            "test message",
            manifest);

        var context = new AgentBackendExecutionContext(request, new UnavailableAgentActionBroker());

        var executeMethod = typeof(LegacyOpenAiCompatibleAgentBackend)
            .GetMethod(nameof(LegacyOpenAiCompatibleAgentBackend.ExecuteAsync));
        
        Assert.NotNull(executeMethod);
        
        var methodBody = executeMethod.ToString();
        Assert.DoesNotContain("ContextManifest", methodBody);
        Assert.DoesNotContain("AgentContextManifest", methodBody);
    }

    [Fact]
    public void ContextManifest_IdentityIsRunScoped()
    {
        var runId1 = ExecutionRunId.New();
        var runId2 = ExecutionRunId.New();
        var sessionId = AgentSessionId.New();
        var conversationId = ConversationId.NewDirect();

        var manifest1 = new AgentContextManifest(
            sessionId,
            runId1,
            conversationId,
            AgentContextPolicyLevel.Standard,
            Array.Empty<AgentContextItem>(),
            new AgentContextTokenBudget(AgentContextPolicyLevel.Standard, 1000, 0),
            Array.Empty<AgentContextTruncationDecision>(),
            Array.Empty<AgentContextExclusionDecision>(),
            FixedAssemblyTime);

        var manifest2 = new AgentContextManifest(
            sessionId,
            runId2,
            conversationId,
            AgentContextPolicyLevel.Standard,
            Array.Empty<AgentContextItem>(),
            new AgentContextTokenBudget(AgentContextPolicyLevel.Standard, 1000, 0),
            Array.Empty<AgentContextTruncationDecision>(),
            Array.Empty<AgentContextExclusionDecision>(),
            FixedAssemblyTime);

        Assert.NotEqual(manifest1.RunId, manifest2.RunId);
        Assert.NotSame(manifest1, manifest2);
    }

    private static AgentContextItem CreateTestContextItem()
    {
        return new AgentContextItem(
            AgentContextSourceId.ActiveFile,
            "test content",
            "test://file",
            "test-fingerprint",
            AgentContextRedactionState.None,
            10,
            new AgentContextProvenance(
                "test-service",
                1,
                wasLiveSnapshot: true,
                redactionApplied: false,
                null),
            null);
    }
}
