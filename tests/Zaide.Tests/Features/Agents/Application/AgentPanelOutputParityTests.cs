using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Tests.Features.Conversations;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Infrastructure;
using Zaide.Tests.Features.Agents;
using Zaide.Tests.Features.Settings.Infrastructure;

namespace Zaide.Tests.Features.Agents.Application;

/// <summary>
/// Refactor 7 M5b: typed direct-conversation output parity, read-only projection,
/// and lifecycle preservation after legacy string ownership removal.
/// </summary>
public sealed class AgentPanelOutputParityTests : IDisposable
{
    private const string LegacyUserHello = "User: Hello";
    private const string LegacyAssistantHelloBack = "Assistant: Hello back";
    private const string LegacyErrorRequestFailed = "Error: Request failed";

    private readonly string _tempDir;

    public AgentPanelOutputParityTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ZaideOutputParity_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    private static (AgentPanelHost Host, AgentPanelState Panel, ConversationStore Store) CreatePanel()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);
        var panel = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");
        return (host, panel, store);
    }

    private AgentExecutionCoordinator CreateCoordinator(
        AgentPanelHost host,
        ConversationStore store,
        HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var (settings, secrets) = CreateSettingsAndSecrets();
        var service = new AgentExecutionService(httpClient, settings, secrets);
        return AgentExecutionTestSupport.CreateCoordinator(host, service, store);
    }

    private (SettingsService settings, TestSecretStore secrets) CreateSettingsAndSecrets()
    {
        var settingsPath = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + "_settings.json");
        var lkgPath = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + "_lkg.json");
        var tmpPath = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + "_tmp.json");

        var llm = new LlmSettings(
            BaseUrl: "https://api.test.com/v1",
            Model: "test-model",
            ApiKeySource: "secret-store");
        var model = SettingsModel.Defaults with { Llm = llm };
        File.WriteAllText(settingsPath, SettingsSerializer.Serialize(model));

        var settingsService = new SettingsService(
            settingsPath,
            lkgPath,
            tmpPath,
            new SettingsMigrator(Array.Empty<ISettingsMigration>()));

        var secrets = new TestSecretStore();
        secrets.Set("llm.apiKey", "test-key");
        return (settingsService, secrets);
    }

    private static void AssertProjectedLinesMatchFrozenLegacy(
        Conversation conversation,
        IReadOnlyList<string> outputHistory)
    {
        var expected = conversation.Entries
            .Where(entry => AgentPanelEntryProjection.TryToOutputHistoryLine(entry, out _))
            .Select(AgentPanelEntryProjection.ToOutputHistoryLine)
            .ToArray();

        Assert.Equal(expected, outputHistory);
    }

    [Fact]
    public async Task DirectSend_Success_ProjectedLinesMatchFrozenLegacyStrings()
    {
        var (host, panel, store) = CreatePanel();
        var coordinator = CreateCoordinator(host, store, new SuccessHandler("Hello back"));

        await coordinator.SendAsync(panel.PanelId, "Hello");

        Assert.True(store.TryGet(panel.ConversationId, out var conversation));
        Assert.Equal(
            new[] { "User: Hello", LegacyAssistantHelloBack },
            panel.OutputHistory.ToArray());
        AssertProjectedLinesMatchFrozenLegacy(conversation!, panel.OutputHistory);
    }

    [Fact]
    public async Task ExecutionFailure_ProjectedLinesMatchFrozenLegacyStrings()
    {
        var (host, panel, store) = CreatePanel();
        var coordinator = CreateCoordinator(
            host,
            store,
            new StatusHandler(HttpStatusCode.InternalServerError, "Server error"));

        await coordinator.SendAsync(panel.PanelId, "Hello");

        Assert.True(store.TryGet(panel.ConversationId, out var conversation));
        Assert.Equal(LegacyUserHello, panel.OutputHistory[0]);
        Assert.StartsWith("Error:", panel.OutputHistory[1]);
        Assert.Contains("500", panel.OutputHistory[1]);
        AssertProjectedLinesMatchFrozenLegacy(conversation!, panel.OutputHistory);
    }

    [Fact]
    public async Task Cancellation_ProjectedLinesMatchFrozenLegacyStrings()
    {
        var (host, panel, store) = CreatePanel();
        var coordinator = CreateCoordinator(
            host,
            store,
            new FaultHandler(new TaskCanceledException("Request was cancelled.")));

        await coordinator.SendAsync(panel.PanelId, "Hello");

        Assert.True(store.TryGet(panel.ConversationId, out var conversation));
        Assert.Equal(2, panel.OutputHistory.Count);
        Assert.Equal(LegacyUserHello, panel.OutputHistory[0]);
        Assert.StartsWith("Error:", panel.OutputHistory[1]);
        AssertProjectedLinesMatchFrozenLegacy(conversation!, panel.OutputHistory);
    }

    [Fact]
    public async Task RoutingFailure_WritesTypedEntryAndProjectsErrorLine()
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(catalog, store);
        var panel = host.GetOrCreatePanelForActor(ActorId.PanelSeed("alpha"));
        var coordinator = CreateCoordinator(host, store, new SuccessHandler("unused"));
        var router = new AgentRouter(new MentionParser(), host, coordinator, catalog, store);

        await router.RouteAndExecuteAsync(panel.PanelId, "@Missing hello");

        Assert.True(store.TryGet(panel.ConversationId, out var conversation));
        Assert.Contains(
            conversation!.Entries,
            e => e.Kind == ConversationEntryKind.RoutingFailure);
        Assert.Single(panel.OutputHistory);
        Assert.Equal("Error: Unknown target", panel.OutputHistory[0]);
    }

    [Fact]
    public async Task RoutedSend_Success_OnlyTargetPanelProjectsFrozenLegacyStrings()
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(catalog, store);
        var source = host.GetOrCreatePanelForActor(ActorId.PanelSeed("alpha"));
        var target = host.GetOrCreatePanelForActor(ActorId.PanelSeed("beta"));
        var coordinator = CreateCoordinator(host, store, new SuccessHandler("Routed reply"));
        var router = new AgentRouter(new MentionParser(), host, coordinator, catalog, store);

        await router.RouteAndExecuteAsync(source.PanelId, "@Beta routed hello");

        Assert.True(store.TryGet(target.ConversationId, out var conversation));
        Assert.Equal(
            new[] { "User: routed hello", "Assistant: Routed reply" },
            target.OutputHistory.ToArray());
        Assert.Empty(source.OutputHistory);
        AssertProjectedLinesMatchFrozenLegacy(conversation!, target.OutputHistory);
    }

    [Fact]
    public async Task MultipleAttempts_PreserveExactOrderingAcrossCorrelatedRuns()
    {
        var (host, panel, store) = CreatePanel();
        var coordinator = CreateCoordinator(host, store, new ToggleHandler());

        await coordinator.SendAsync(panel.PanelId, "First");
        await coordinator.SendAsync(panel.PanelId, "Second");

        Assert.True(store.TryGet(panel.ConversationId, out var conversation));
        Assert.Equal(4, conversation!.Entries.Count);
        Assert.Equal(4, panel.OutputHistory.Count);
        AssertProjectedLinesMatchFrozenLegacy(conversation, panel.OutputHistory);
    }

    [Fact]
    public async Task DirectSend_Success_CorrelatesTypedEntriesWithStructuredRunId()
    {
        var (host, panel, store) = CreatePanel();
        var coordinator = CreateCoordinator(host, store, new SuccessHandler("Hello back"));

        var result = await coordinator.SendAsync(panel.PanelId, "Hi");

        Assert.NotNull(result);
        Assert.True(store.TryGet(panel.ConversationId, out var conversation));
        Assert.All(
            conversation!.Entries,
            entry =>
            {
                Assert.NotNull(entry.CorrelationId);
                Assert.Equal(result!.Run.Id.Value, entry.CorrelationId!.Value.Value);
            });
    }

    [Fact]
    public async Task DuplicateInFlight_DoesNotDuplicateTypedOrProjectedWrites()
    {
        var (host, panel, store) = CreatePanel();
        var coordinator = CreateCoordinator(
            host,
            store,
            new DelayedSuccessHandler(TimeSpan.FromMilliseconds(200), "Slow"));

        var first = coordinator.SendAsync(panel.PanelId, "First");
        var second = await coordinator.SendAsync(panel.PanelId, "Second");

        Assert.NotNull(second);
        Assert.Equal(ExecutionRunOutcome.Rejected, second!.Run.Outcome);
        await first;

        Assert.True(store.TryGet(panel.ConversationId, out var conversation));
        Assert.Equal(2, conversation!.Entries.Count);
        Assert.Equal(2, panel.OutputHistory.Count);
    }

    [Fact]
    public async Task ClosePanelDuringExecution_RetainsAuthoritativeTerminalEntry()
    {
        var (host, panel, store) = CreatePanel();
        var coordinator = CreateCoordinator(
            host,
            store,
            new DelayedSuccessHandler(TimeSpan.FromMilliseconds(200), "After close"));
        var conversationId = panel.ConversationId;

        var execution = coordinator.SendAsync(panel.PanelId, "Hello");
        host.ClosePanel(panel.PanelId);

        await execution;

        Assert.Empty(host.Panels);
        Assert.True(store.TryGet(conversationId, out var conversation));
        Assert.Equal(2, conversation!.Entries.Count);
        Assert.Equal(ConversationEntryKind.AssistantResponse, conversation.Entries[1].Kind);
        Assert.Equal("After close", conversation.Entries[1].Content);
    }

    [Fact]
    public async Task ClosePanelDuringExecution_RemovesProjectionWithoutTerminalLine()
    {
        var (host, panel, store) = CreatePanel();
        var coordinator = CreateCoordinator(
            host,
            store,
            new DelayedSuccessHandler(TimeSpan.FromMilliseconds(200), "After close"));
        var conversationId = panel.ConversationId;

        var execution = coordinator.SendAsync(panel.PanelId, "Hello");
        host.ClosePanel(panel.PanelId);
        var outputCountAtClose = panel.OutputHistory.Count;
        await execution;

        Assert.True(store.TryGet(conversationId, out var conversation));
        Assert.Equal(2, conversation!.Entries.Count);
        Assert.Equal(ConversationEntryKind.AssistantResponse, conversation.Entries[1].Kind);
        Assert.Equal("After close", conversation.Entries[1].Content);
        Assert.Equal(1, outputCountAtClose);
        Assert.Equal(outputCountAtClose, panel.OutputHistory.Count);
        Assert.Equal("User: Hello", panel.OutputHistory[0]);
    }

    [Fact]
    public async Task ClosePanelDuringExecution_OtherOpenPanelsContinueProjecting()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);
        var closing = host.CreatePanel("agent-1", "Alpha", "avatar_a");
        var remaining = host.CreatePanel("agent-2", "Beta", "avatar_b");
        var coordinator = CreateCoordinator(
            host,
            store,
            new ToggleHandler());

        var closingExecution = coordinator.SendAsync(closing.PanelId, "Closing");
        host.ClosePanel(closing.PanelId);
        await closingExecution;

        await coordinator.SendAsync(remaining.PanelId, "Remaining");

        Assert.True(store.TryGet(remaining.ConversationId, out var remainingConversation));
        Assert.Equal(2, remainingConversation!.Entries.Count);
        Assert.Equal(
            new[] { "User: Remaining", "Assistant: Recovered" },
            remaining.OutputHistory.ToArray());
        AssertProjectedLinesMatchFrozenLegacy(remainingConversation, remaining.OutputHistory);
    }

    [Fact]
    public void RepeatedCreateAndClose_DoesNotAccumulateOutputProjections()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);

        for (var i = 0; i < 5; i++)
        {
            var panel = host.CreatePanel($"agent-{i}", $"Agent {i}", $"avatar_{i}");
            host.ClosePanel(panel.PanelId);
        }

        Assert.Equal(0, GetOutputProjectionCount(host));

        var active = host.CreatePanel("agent-final", "Final", "avatar_final");
        Assert.Equal(1, GetOutputProjectionCount(host));

        AgentPanelTestSupport.AppendUserChat(store, active, "live");
        Assert.Equal("User: live", active.OutputHistory.Single());

        host.ClosePanel(active.PanelId);
        Assert.Equal(0, GetOutputProjectionCount(host));
    }

    private static int GetOutputProjectionCount(AgentPanelHost host)
    {
        var field = typeof(AgentPanelHost).GetField(
            "_outputProjections",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var projections = (Dictionary<AgentPanelState, AgentPanelOutputHistoryProjection>)field!.GetValue(host)!;
        return projections.Count;
    }

    [Fact]
    public async Task SwitchActivePanelDuringExecution_DoesNotRetargetConversation()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);
        var first = host.CreatePanel("agent-1", "Alpha", "avatar_a");
        var second = host.CreatePanel("agent-2", "Beta", "avatar_b");
        var coordinator = CreateCoordinator(
            host,
            store,
            new DelayedSuccessHandler(TimeSpan.FromMilliseconds(200), "First only"));
        var firstConversationId = first.ConversationId;

        var execution = coordinator.SendAsync(first.PanelId, "Hello");
        host.ActivatePanel(second.PanelId);
        await execution;

        Assert.True(store.TryGet(firstConversationId, out var conversation));
        Assert.Equal(2, conversation!.Entries.Count);
        Assert.Empty(second.OutputHistory);
        AssertProjectedLinesMatchFrozenLegacy(conversation, first.OutputHistory);
    }

    [Fact]
    public void OutputHistory_IsReadOnlyAndCannotDivergeFromConversationEntries()
    {
        var store = ConversationsTestSupport.CreateStore();
        var panel = AgentPanelTestSupport.CreatePanelState(store: store);

        Assert.Throws<NotSupportedException>(() =>
            ((IList)panel.OutputHistory).Add("User: forged"));

        AgentPanelTestSupport.AppendUserChat(store, panel, "authoritative");
        Assert.Equal("User: authoritative", panel.OutputHistory.Single());

        Assert.True(store.TryGet(panel.ConversationId, out var conversation));
        AssertProjectedLinesMatchFrozenLegacy(conversation!, panel.OutputHistory);
    }

    [Fact]
    public void UnsupportedConversationEntryKinds_AreNotRenderedInPanelOutput()
    {
        var store = ConversationsTestSupport.CreateStore();
        var panel = AgentPanelTestSupport.CreatePanelState(store: store);
        var runId = ExecutionRunId.New();
        var correlation = ExecutionRunCorrelation.ToEntryCorrelation(runId);

        store.AppendEntry(
            panel.ConversationId,
            ConversationEntry.UserChat(
                ConversationEntryId.New(),
                ActorId.HumanUser,
                DateTimeOffset.UtcNow,
                "visible",
                correlation));
        store.AppendEntry(
            panel.ConversationId,
            ConversationEntry.ChannelEvent(
                ConversationEntryId.New(),
                ActorId.HumanUser,
                DateTimeOffset.UtcNow,
                "channel switch",
                correlation));
        store.AppendEntry(
            panel.ConversationId,
            ConversationEntry.SystemNotification(
                ConversationEntryId.New(),
                ActorId.HumanUser,
                DateTimeOffset.UtcNow,
                "system notice",
                correlation));
        store.AppendEntry(
            panel.ConversationId,
            ConversationEntry.RoutingFailure(
                ConversationEntryId.New(),
                panel.ActorId,
                DateTimeOffset.UtcNow,
                "routing failed",
                correlation));
        store.AppendEntry(
            panel.ConversationId,
            ConversationEntry.AssistantResponse(
                ConversationEntryId.New(),
                panel.ActorId,
                DateTimeOffset.UtcNow,
                "visible reply",
                correlation));

        // Channel/system kinds remain unrendered; routing failure projects as Error.
        Assert.Equal(
            new[] { "User: visible", "Error: routing failed", "Assistant: visible reply" },
            panel.OutputHistory.ToArray());
    }

    [Fact]
    public void ProductionCoordinator_DoesNotWriteLegacyOutputHistoryStrings()
    {
        var repoRoot = Zaide.Tests.Architecture.ArchitectureInventoryReader.ResolveRepositoryRoot();
        var writerSource = File.ReadAllText(
            Path.Combine(repoRoot, "src/Features/Agents/Application/AgentPanelDirectConversationWriter.cs"));
        var coordinatorSource = File.ReadAllText(
            Path.Combine(repoRoot, "src/Features/Agents/Application/AgentExecutionCoordinator.cs"));

        Assert.DoesNotContain("OutputHistory", writerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OutputHistory", coordinatorSource, StringComparison.Ordinal);
    }

    private sealed class ToggleHandler : HttpMessageHandler
    {
        private bool _hasFailed;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (!_hasFailed)
            {
                _hasFailed = true;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("Server error", Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    choices = new[] { new { message = new { content = "Recovered" }, finish_reason = "stop" } }
                }), Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class SuccessHandler : HttpMessageHandler
    {
        private readonly string _content;

        public SuccessHandler(string content) => _content = content;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    choices = new[] { new { message = new { content = _content }, finish_reason = "stop" } }
                }), Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class StatusHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _code;
        private readonly string _body;

        public StatusHandler(HttpStatusCode code, string body)
        {
            _code = code;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            return Task.FromResult(new HttpResponseMessage(_code)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class FaultHandler : HttpMessageHandler
    {
        private readonly Exception _ex;

        public FaultHandler(Exception ex) => _ex = ex;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            throw _ex;
        }
    }

    private sealed class DelayedSuccessHandler : HttpMessageHandler
    {
        private readonly TimeSpan _delay;
        private readonly string _content;

        public DelayedSuccessHandler(TimeSpan delay, string content)
        {
            _delay = delay;
            _content = content;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await Task.Delay(_delay, ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    choices = new[] { new { message = new { content = _content }, finish_reason = "stop" } }
                }), Encoding.UTF8, "application/json")
            };
        }
    }
}
