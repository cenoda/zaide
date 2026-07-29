using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Xunit;
using Zaide;
using Zaide.App.Composition;
using Zaide.App.Composition.Registration;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;
using Zaide.Features.Workspace.Contracts;
using Zaide.Features.Workspace.Domain;
using Zaide.Tests.Features.Conversations;

namespace Zaide.Tests.Features.Agents;

/// <summary>
/// Phase 19 M5 — Townhall structured activity projection through the broker-event path.
/// </summary>
public sealed class Phase19TownhallProjectionTests : IDisposable
{
    private readonly string _root;
    private readonly FakeWorkspaceActionAuthority _workspaceAuthority;
    private readonly ScriptedNativeHarnessProviderTransport _transport;
    private readonly ServiceProvider _provider;
    private readonly List<AgentEvent> _capturedEvents = new();

    public Phase19TownhallProjectionTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "Phase19Townhall_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "note.txt"), "townhall-content");

        _workspaceAuthority = new FakeWorkspaceActionAuthority(
            FakeWorkspaceActionAuthority.CreateScopeFromDirectory(_root));
        _transport = new ScriptedNativeHarnessProviderTransport();

        var services = new ServiceCollection();
        Program.ConfigureServices(services);
        ReplaceSingleton(services, typeof(INativeHarnessProviderTransport), _transport);
        ReplaceSingleton(services, typeof(IAgentPermissionReviewService), new AllowingPermissionReviewService());
        ReplaceSingleton(services, typeof(IWorkspaceActionAuthority), _workspaceAuthority);
        ReplaceSingleton(
            services,
            typeof(INativeHarnessProviderOptionsSource),
            new FixedNativeHarnessProviderOptionsSource());
        services.AddSingleton<IScheduler>(_ => CurrentThreadScheduler.Instance);

        _provider = services.BuildServiceProvider();
        _provider.GetRequiredService<AgentEventStream>().Events.Subscribe(_capturedEvents.Add);
        _ = _provider.GetRequiredService<AgentConversationEventProjection>();
    }

    public void Dispose()
    {
        _provider.Dispose();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    [Fact]
    public void ActionResultReported_ProjectsStructuredSystemNotificationThroughConversationProjection()
    {
        var store = ConversationsTestSupport.CreateStore();
        var stream = new AgentEventStream();
        var catalog = ConversationsTestSupport.CreateCatalog();
        using var projection = new AgentConversationEventProjection(stream.Events, store, catalog);

        var conversation = store.CreateDirectConversation(ActorId.HumanUser, ActorId.TownhallAgent);
        var sessionId = AgentSessionId.New();
        var runId = ExecutionRunId.New();
        var actionId = AgentActionId.New();
        var summary = new AgentActionAuditSummary("result Succeeded; note.txt");

        stream.Publish(CreateActionResultReportedEvent(
            sessionId,
            runId,
            conversation.Id,
            actionId,
            AgentActionKind.ReadFile,
            AgentActionResultKind.Succeeded,
            AgentActivityEvidenceLevel.ZaideExecuted,
            summary));

        var entry = Assert.Single(conversation.Entries);
        Assert.Equal(ConversationEntryKind.SystemNotification, entry.Kind);
        Assert.StartsWith("zaide-action|v1|", entry.Content, StringComparison.Ordinal);
        Assert.Contains("Read file", entry.Content, StringComparison.Ordinal);

        var message = TownhallEntryProjection.ToTownhallMessage(entry, catalog);
        Assert.Equal(TownhallMessageKind.ToolResult, message.Kind);
        Assert.Contains("Tool result: Read file", message.Content, StringComparison.Ordinal);
        Assert.Contains("[Zaide-executed]", message.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NativeHarnessToolActivity_ReachesTownhallViaBrokerEventPath()
    {
        _transport.Enqueue(NativeHarnessProviderResponse.Success(
            assistantContent: null,
            toolCalls: new[]
            {
                new NativeHarnessProviderToolCall(
                    NativeHarnessToolCallId.FromValue("call-townhall-1"),
                    NativeHarnessProviderProtocol.ReadFileToolName,
                    """{"path":"note.txt"}"""),
            }));
        _transport.Enqueue(NativeHarnessProviderResponse.Success("townhall read complete"));

        var session = _provider.GetRequiredService<IAgentSessionService>();
        var store = _provider.GetRequiredService<IConversationStore>();
        var catalog = ConversationsTestSupport.CreateCatalog();
        var vm = ConversationsTestSupport.CreateTownhallViewModel(store: store, catalog: catalog);
        vm.OpenDirectConversationCommand.Execute(ActorId.TownhallAgent).Subscribe();
        var conversation = store.GetOrCreateDirectConversation(
            catalog.CanonicalHuman.Id,
            ActorId.TownhallAgent);

        var snapshot = await session.SendAsync(
            conversation.Id,
            ActorId.HumanUser,
            ActorId.TownhallAgent,
            AgentBackendIds.NativeHarness,
            ConversationEntryId.New(),
            "read for townhall",
            CancellationToken.None);

        Assert.Equal(AgentRunStatus.Completed, snapshot.Status);
        Assert.Contains(
            _capturedEvents,
            e => e.Kind == AgentEventKind.ActionResultReported);

        Assert.True(store.TryGet(conversation.Id, out conversation));
        var actionEntry = conversation!.Entries.Single(
            entry => entry.Kind == ConversationEntryKind.SystemNotification
                     && entry.Content.StartsWith("zaide-action|v1|", StringComparison.Ordinal));
        Assert.Contains("Read file", actionEntry.Content, StringComparison.Ordinal);

        var townhallMessage = vm.Messages.Single(
            message => message.Content.Contains("Tool result: Read file", StringComparison.Ordinal));
        Assert.Equal(TownhallMessageKind.ToolResult, townhallMessage.Kind);
        Assert.Contains("[Zaide-executed]", townhallMessage.Content, StringComparison.Ordinal);
        Assert.Contains("result Succeeded", townhallMessage.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void ActionActivity_SuccessDenialAndBoundedEvidence_AreDistinguishableInTownhall()
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var timestamp = DateTimeOffset.Parse("2026-07-28T10:00:00Z");

        var successEntry = ConversationEntry.SystemNotification(
            ConversationEntryId.New(),
            ActorId.TownhallAgent,
            timestamp,
            AgentConversationEventProjection.FormatActionResultEntryContent(
                AgentActionKind.ReadFile,
                AgentActionResultKind.Succeeded,
                AgentActivityEvidenceLevel.ZaideExecuted,
                new AgentActionAuditSummary("result Succeeded; note.txt")));
        var deniedEntry = ConversationEntry.SystemNotification(
            ConversationEntryId.New(),
            ActorId.TownhallAgent,
            timestamp,
            AgentConversationEventProjection.FormatActionResultEntryContent(
                AgentActionKind.ExecuteCommand,
                AgentActionResultKind.Denied,
                AgentActivityEvidenceLevel.ZaideMediated,
                new AgentActionAuditSummary("result Denied; PermissionDenied")));
        var boundedEntry = ConversationEntry.SystemNotification(
            ConversationEntryId.New(),
            ActorId.TownhallAgent,
            timestamp,
            AgentConversationEventProjection.FormatActionResultEntryContent(
                AgentActionKind.ReadFile,
                AgentActionResultKind.Succeeded,
                AgentActivityEvidenceLevel.ZaideExecuted,
                new AgentActionAuditSummary("token=secret-value", wasRedacted: true)));

        var successMessage = TownhallEntryProjection.ToTownhallMessage(successEntry, catalog);
        var deniedMessage = TownhallEntryProjection.ToTownhallMessage(deniedEntry, catalog);
        var boundedMessage = TownhallEntryProjection.ToTownhallMessage(boundedEntry, catalog);

        Assert.Equal(TownhallMessageKind.ToolResult, successMessage.Kind);
        Assert.Contains("[Zaide-executed]", successMessage.Content, StringComparison.Ordinal);

        Assert.Equal(TownhallMessageKind.AgentError, deniedMessage.Kind);
        Assert.Contains("Action denied: Run command", deniedMessage.Content, StringComparison.Ordinal);
        Assert.Contains("[Zaide-mediated]", deniedMessage.Content, StringComparison.Ordinal);

        Assert.Contains("[redacted]", boundedMessage.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", boundedMessage.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void ActionActivity_UsesExistingBrokerPath_NoNewAgentEventKind()
    {
        var eventKindCount = Enum.GetValues<AgentEventKind>().Length;
        Assert.Equal(28, eventKindCount);
        Assert.Contains(AgentEventKind.ActionResultReported, Enum.GetValues<AgentEventKind>());

        var store = ConversationsTestSupport.CreateStore();
        var stream = new AgentEventStream();
        using var projection = new AgentConversationEventProjection(
            stream.Events,
            store,
            ConversationsTestSupport.CreateCatalog());

        var conversation = store.CreateDirectConversation(ActorId.HumanUser, ActorId.TownhallAgent);
        var sessionId = AgentSessionId.New();
        var runId = ExecutionRunId.New();

        stream.Publish(CreateActionResultReportedEvent(
            sessionId,
            runId,
            conversation.Id,
            AgentActionId.New(),
            AgentActionKind.ReadFile,
            AgentActionResultKind.Succeeded,
            AgentActivityEvidenceLevel.ZaideExecuted,
            new AgentActionAuditSummary("result Succeeded; note.txt")));

        Assert.Single(
            conversation.Entries,
            entry => entry.Kind == ConversationEntryKind.SystemNotification
                     && entry.Content.StartsWith("zaide-action|v1|", StringComparison.Ordinal));
        Assert.DoesNotContain(
            Enum.GetValues<AgentEventKind>(),
            kind => kind.ToString().Contains("Townhall", StringComparison.Ordinal));
    }

    private static AgentEvent CreateActionResultReportedEvent(
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        AgentActionId actionId,
        AgentActionKind actionKind,
        AgentActionResultKind resultKind,
        AgentActivityEvidenceLevel evidenceLevel,
        AgentActionAuditSummary summary)
    {
        return new AgentEvent(
            AgentEventId.New(),
            AgentEvent.CurrentSchemaVersion,
            sessionId,
            runId,
            conversationId,
            AgentBackendIds.NativeHarness,
            sequence: 5,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            causationEventId: null,
            evidenceLevel,
            AgentEventKind.ActionResultReported,
            new AgentActionFactPayload(
                actionId,
                AgentActionAttemptId.New(),
                actionKind,
                WorkspaceIdentity.New(),
                WorkspaceGeneration.Initial,
                summary,
                resultKind: resultKind));
    }

    private static void ReplaceSingleton(IServiceCollection services, Type serviceType, object instance)
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == serviceType);
        if (descriptor is not null)
        {
            services.Remove(descriptor);
        }

        services.AddSingleton(serviceType, instance);
    }

    private sealed class FixedNativeHarnessProviderOptionsSource : INativeHarnessProviderOptionsSource
    {
        public AgentExecutionOptions? ResolveOptions() =>
            new()
            {
                BaseUrl = "https://api.test.com/v1",
                ApiKey = "townhall-test-key",
                Model = "townhall-test-model",
            };
    }

    private sealed class AllowingPermissionReviewService : IAgentPermissionReviewService
    {
        public ValueTask<AgentPermissionDecision> RequestDecisionAsync(
            AgentActionRequest request,
            AgentActionDisplaySummary displaySummary,
            WorkspaceActionScope? workspaceScope,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AgentPermissionDecision(
                AgentPermissionDecisionId.New(),
                request.Fingerprint,
                AgentActionPermissionClassification.RequiresUserDecision,
                AgentPermissionDecisionStatus.Published,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                isAllow: true));
    }
}
