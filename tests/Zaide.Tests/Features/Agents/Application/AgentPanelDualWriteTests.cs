using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
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
/// Refactor 7 M5a: authoritative typed direct-conversation dual-write and
/// legacy <see cref="AgentPanelState.OutputHistory"/> projection parity.
/// </summary>
public sealed class AgentPanelDualWriteTests : IDisposable
{
    private readonly string _tempDir;

    public AgentPanelDualWriteTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ZaideDualWrite_" + Guid.NewGuid().ToString("N"));
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

    [Fact]
    public async Task DirectSend_Success_WritesTypedEntriesAndProjectsOutputHistory()
    {
        var (host, panel, store) = CreatePanel();
        var coordinator = CreateCoordinator(host, store, new SuccessHandler("Hello back"));

        await coordinator.SendAsync(panel.PanelId, "Hi");

        Assert.True(store.TryGet(panel.ConversationId, out var conversation));
        Assert.Equal(2, conversation!.Entries.Count);
        Assert.Equal(ConversationEntryKind.UserChat, conversation.Entries[0].Kind);
        Assert.Equal("Hi", conversation.Entries[0].Content);
        Assert.Equal(ActorId.HumanUser, conversation.Entries[0].Author);
        Assert.Equal(ConversationEntryKind.AssistantResponse, conversation.Entries[1].Kind);
        Assert.Equal("Hello back", conversation.Entries[1].Content);
        Assert.Equal(panel.ActorId, conversation.Entries[1].Author);

        Assert.Equal(
            conversation.Entries.Select(AgentPanelEntryProjection.ToOutputHistoryLine),
            panel.OutputHistory);
    }

    [Fact]
    public async Task RoutedSend_Success_TargetsTargetPanelConversation()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);
        var source = host.CreatePanel("agent-1", "Alpha", "avatar_a");
        var target = host.CreatePanel("agent-2", "Beta", "avatar_b");
        var coordinator = CreateCoordinator(host, store, new SuccessHandler("Routed reply"));
        var router = new AgentRouter(new MentionParser(), host, coordinator);

        await router.RouteAndExecuteAsync(source.PanelId, "@Beta routed hello");

        Assert.True(store.TryGet(target.ConversationId, out var conversation));
        Assert.Equal(2, conversation!.Entries.Count);
        Assert.Equal("routed hello", conversation.Entries[0].Content);
        Assert.Equal("Routed reply", conversation.Entries[1].Content);
        Assert.Empty(source.OutputHistory);
        Assert.Equal(
            conversation.Entries.Select(AgentPanelEntryProjection.ToOutputHistoryLine),
            target.OutputHistory);
    }

    [Fact]
    public async Task RoutingFailure_DoesNotWriteDirectConversationOrOutputHistory()
    {
        var (host, panel, store) = CreatePanel();
        var coordinator = CreateCoordinator(host, store, new SuccessHandler("unused"));
        var router = new AgentRouter(new MentionParser(), host, coordinator);

        await router.RouteAndExecuteAsync(panel.PanelId, "@Missing hello");

        Assert.True(store.TryGet(panel.ConversationId, out var conversation));
        Assert.Empty(conversation!.Entries);
        Assert.Empty(panel.OutputHistory);
    }

    [Fact]
    public async Task ExecutionFailure_WritesTypedFailureAndProjectsErrorPrefix()
    {
        var (host, panel, store) = CreatePanel();
        var coordinator = CreateCoordinator(
            host,
            store,
            new StatusHandler(HttpStatusCode.InternalServerError, "Server error"));

        await coordinator.SendAsync(panel.PanelId, "Hello");

        Assert.True(store.TryGet(panel.ConversationId, out var conversation));
        Assert.Equal(2, conversation!.Entries.Count);
        Assert.Equal(ConversationEntryKind.ExecutionFailure, conversation.Entries[1].Kind);
        Assert.Contains("500", conversation.Entries[1].Content);
        Assert.Equal("User: Hello", panel.OutputHistory[0]);
        Assert.StartsWith("Error:", panel.OutputHistory[1]);
        Assert.Contains("500", panel.OutputHistory[1]);
    }

    [Fact]
    public async Task Cancellation_WritesUserAndTerminalFailureEntries()
    {
        var (host, panel, store) = CreatePanel();
        var coordinator = CreateCoordinator(
            host,
            store,
            new FaultHandler(new TaskCanceledException("Request was cancelled.")));

        await coordinator.SendAsync(panel.PanelId, "Hello");

        Assert.True(store.TryGet(panel.ConversationId, out var conversation));
        Assert.Equal(2, conversation!.Entries.Count);
        Assert.Equal(ConversationEntryKind.ExecutionFailure, conversation.Entries[1].Kind);
        Assert.Contains("cancelled", conversation.Entries[1].Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, panel.OutputHistory.Count);
    }

    [Fact]
    public async Task DuplicateInFlight_DoesNotDuplicateTypedOrLegacyWrites()
    {
        var (host, panel, store) = CreatePanel();
        var coordinator = CreateCoordinator(
            host,
            store,
            new DelayedSuccessHandler(TimeSpan.FromMilliseconds(200), "Slow"));

        var first = coordinator.SendAsync(panel.PanelId, "First");
        var second = await coordinator.SendAsync(panel.PanelId, "Second");

        Assert.Null(second);
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
    public async Task ClosePanelDuringExecution_PreservesRetainedDirectConversation()
    {
        var (host, panel, store) = CreatePanel();
        var coordinator = CreateCoordinator(
            host,
            store,
            new DelayedSuccessHandler(TimeSpan.FromMilliseconds(200), "Retained"));
        var conversationId = panel.ConversationId;

        var execution = coordinator.SendAsync(panel.PanelId, "Hello");
        host.ClosePanel(panel.PanelId);
        await execution;

        Assert.True(store.TryGet(conversationId, out _));
        Assert.Equal(2, panel.OutputHistory.Count);
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
