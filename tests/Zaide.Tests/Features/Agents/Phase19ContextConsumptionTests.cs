using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Infrastructure;
using Zaide.Tests.Features.Conversations;
using Zaide.Tests.Features.Settings.Infrastructure;

namespace Zaide.Tests.Features.Agents;

public sealed class Phase19ContextConsumptionTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<IDisposable> _disposables = new();

    public Phase19ContextConsumptionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Phase19Context_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            try { disposable.Dispose(); } catch { /* best-effort */ }
        }

        _disposables.Clear();

        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    [Fact]
    public async Task Phase19ContextConsumption_ManifestItems_AppearInProviderSystemPrompt()
    {
        var manifest = CreateManifest(
            new AgentContextItem(
                AgentContextSourceId.ActiveFile,
                content: "class Program {}",
                scopeDescriptor: "workspace/src/Program.cs",
                fingerprint: "fp-active",
                redactionState: AgentContextRedactionState.None,
                estimatedTokenCount: 4,
                provenance: CreateProvenance()));

        var transport = new ScriptedNativeHarnessProviderTransport();
        transport.Enqueue(NativeHarnessProviderResponse.Success("context consumed"));

        var backend = CreateBackend(transport);
        var request = CreateRequest("use context", manifest);
        await CollectEventsAsync(backend, CreateContext(request, new RecordingAgentActionBroker()));

        var systemMessage = transport.LastRequest!.Messages[0];
        Assert.Equal("system", systemMessage.Role);
        Assert.Contains("class Program {}", systemMessage.Content, StringComparison.Ordinal);
        Assert.Contains(AgentContextSourceId.ActiveFile.Value, systemMessage.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase19ContextConsumption_ProcessingFailedItems_AreExcludedFromSystemPrompt()
    {
        var manifest = CreateManifest(
            new AgentContextItem(
                AgentContextSourceId.ActiveFile,
                content: string.Empty,
                scopeDescriptor: "workspace/src/Program.cs",
                fingerprint: "fp-failed",
                redactionState: AgentContextRedactionState.ProcessingFailed,
                estimatedTokenCount: 0,
                provenance: CreateProvenance()),
            new AgentContextItem(
                AgentContextSourceId.ProjectContext,
                content: "visible project context",
                scopeDescriptor: "workspace",
                fingerprint: "fp-project",
                redactionState: AgentContextRedactionState.None,
                estimatedTokenCount: 8,
                provenance: CreateProvenance()));

        var prompt = NativeHarnessSystemPromptBuilder.Build(manifest);

        Assert.DoesNotContain("fp-failed", prompt, StringComparison.Ordinal);
        Assert.Contains("visible project context", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase19ContextConsumption_HardExclusions_AreRecordedInSystemPrompt()
    {
        var manifest = new AgentContextManifest(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            AgentContextPolicyLevel.Standard,
            Array.Empty<AgentContextItem>(),
            CreateTokenBudget(),
            Array.Empty<AgentContextTruncationDecision>(),
            new[]
            {
                new AgentContextExclusionDecision(
                    sourceId: default,
                    hardExclusionId: AgentContextHardExclusionId.TerminalScrollback,
                    reason: "Always excluded.",
                    isHardExclusion: true),
            },
            DateTimeOffset.UtcNow);

        var prompt = NativeHarnessSystemPromptBuilder.Build(manifest);

        Assert.Contains("Hard exclusion applied", prompt, StringComparison.Ordinal);
        Assert.Contains(AgentContextHardExclusionId.TerminalScrollback.Value, prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Phase19ContextConsumption_PriorConversationReplay_IsIncludedInProviderMessages()
    {
        var store = ConversationsTestSupport.CreateStore();
        var conversation = store.GetOrCreateDirectConversation(
            ActorId.FromValue("actor:user"),
            ActorId.FromValue("actor:agent"));
        var priorUser = ConversationEntry.UserChat(
            ConversationEntryId.FromValue("entry:prior-user"),
            ActorId.FromValue("actor:user"),
            DateTimeOffset.UtcNow.AddMinutes(-5),
            "prior question");
        var priorAssistant = ConversationEntry.AssistantResponse(
            ConversationEntryId.FromValue("entry:prior-assistant"),
            ActorId.FromValue("actor:agent"),
            DateTimeOffset.UtcNow.AddMinutes(-4),
            "prior answer");
        var current = ConversationEntry.UserChat(
            ConversationEntryId.FromValue("entry:current"),
            ActorId.FromValue("actor:user"),
            DateTimeOffset.UtcNow,
            "current question");
        store.AppendEntry(conversation.Id, priorUser);
        store.AppendEntry(conversation.Id, priorAssistant);
        store.AppendEntry(conversation.Id, current);

        var transport = new ScriptedNativeHarnessProviderTransport();
        transport.Enqueue(NativeHarnessProviderResponse.Success("replayed"));

        var backend = CreateBackend(transport, store);
        var request = new AgentBackendRequest(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            conversation.Id,
            ActorId.FromValue("actor:user"),
            ActorId.FromValue("actor:agent"),
            current.Id,
            current.Content);
        await CollectEventsAsync(backend, CreateContext(request, new RecordingAgentActionBroker()));

        var messages = transport.LastRequest!.Messages;
        Assert.Contains(messages, message => message.Role == "user" && message.Content == "prior question");
        Assert.Contains(messages, message => message.Role == "assistant" && message.Content == "prior answer");
        Assert.Contains(messages, message => message.Role == "user" && message.Content == "current question");
        Assert.DoesNotContain(messages, message => message.Content == current.Content && message.Role == "assistant");
    }

    [Fact]
    public async Task Phase19ContextConsumption_NullManifest_StillBuildsToolInstructions()
    {
        var transport = new ScriptedNativeHarnessProviderTransport();
        transport.Enqueue(NativeHarnessProviderResponse.Success("no manifest"));

        var backend = CreateBackend(transport);
        await CollectEventsAsync(
            backend,
            CreateContext(CreateRequest("hello", contextManifest: null), new RecordingAgentActionBroker()));

        var systemMessage = transport.LastRequest!.Messages[0];
        Assert.Contains(NativeHarnessProviderProtocol.ReadFileToolName, systemMessage.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("IDE context policy", systemMessage.Content, StringComparison.Ordinal);
    }

    private NativeHarnessAgentBackend CreateBackend(
        ScriptedNativeHarnessProviderTransport transport,
        IConversationStore? store = null)
    {
        var executionService = CreateExecutionService();
        return new NativeHarnessAgentBackend(
            executionService,
            transport,
            new NativeHarnessPriorConversationReader(store ?? ConversationsTestSupport.CreateStore()));
    }

    private AgentExecutionService CreateExecutionService() =>
        Phase19HarnessTestFactory.CreateExecutionService(_tempDir, disposableTracker: _disposables);

    private static AgentBackendRequest CreateRequest(
        string messageText,
        AgentContextManifest? contextManifest) =>
        new(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            ActorId.FromValue("actor:user"),
            ActorId.FromValue("actor:agent"),
            ConversationEntryId.FromValue("entry:current"),
            messageText,
            contextManifest);

    private static AgentBackendExecutionContext CreateContext(
        AgentBackendRequest request,
        IAgentActionBroker broker) =>
        new(request, broker);

    private static async Task<IReadOnlyList<AgentBackendEvent>> CollectEventsAsync(
        NativeHarnessAgentBackend backend,
        AgentBackendExecutionContext context)
    {
        var events = new List<AgentBackendEvent>();
        await foreach (var backendEvent in backend.ExecuteAsync(context, CancellationToken.None))
        {
            events.Add(backendEvent);
        }

        return events;
    }

    private static AgentContextManifest CreateManifest(params AgentContextItem[] items) =>
        new(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            AgentContextPolicyLevel.Standard,
            items,
            CreateTokenBudget(),
            Array.Empty<AgentContextTruncationDecision>(),
            Array.Empty<AgentContextExclusionDecision>(),
            DateTimeOffset.UtcNow);

    private static AgentContextTokenBudget CreateTokenBudget() =>
        new(AgentContextPolicyLevel.Standard, requestedBudget: 4_000, actualTokenCount: 0);

    private static AgentContextProvenance CreateProvenance() =>
        new(
            sourceServiceIdentity: "service:test",
            snapshotGeneration: 1,
            wasLiveSnapshot: true,
            redactionApplied: false);
}
