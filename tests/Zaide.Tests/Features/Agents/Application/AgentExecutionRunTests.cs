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
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Domain;
using Zaide.Tests.Features.Agents;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Infrastructure;
using Zaide.Tests.Features.Settings.Infrastructure;

namespace Zaide.Tests.Features.Agents.Application;

/// <summary>
/// Refactor 7 M4: structured execution-run correlation and coordinator result invariants.
/// </summary>
public sealed class AgentExecutionRunTests : IDisposable
{
    private readonly string _tempDir;

    public AgentExecutionRunTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ZaideRunTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    private static (AgentPanelHost Host, AgentPanelState Panel, ConversationStore Store) CreateHostWithPanel()
    {
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(store: store);
        var panel = host.CreatePanel("agent-1", "Test Agent", "avatar_test");
        return (host, panel, store);
    }

    private (SettingsService settings, TestSecretStore secrets) CreateSettingsAndSecrets(
        AgentExecutionOptions options)
    {
        var settingsPath = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + "_settings.json");
        var lkgPath = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + "_lkg.json");
        var tmpPath = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + "_tmp.json");

        var llm = new LlmSettings(
            BaseUrl: options.BaseUrl,
            Model: options.Model,
            ApiKeySource: "secret-store");
        var model = SettingsModel.Defaults with { Llm = llm };
        File.WriteAllText(settingsPath, SettingsSerializer.Serialize(model));

        var settingsService = new SettingsService(
            settingsPath,
            lkgPath,
            tmpPath,
            new SettingsMigrator(Array.Empty<ISettingsMigration>()));

        var secrets = new TestSecretStore();
        if (!string.IsNullOrEmpty(options.ApiKey))
            secrets.Set("llm.apiKey", options.ApiKey);

        return (settingsService, secrets);
    }

    private AgentExecutionCoordinator CreateCoordinator(
        AgentPanelHost host,
        ConversationStore store,
        HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var (settings, secrets) = CreateSettingsAndSecrets(new AgentExecutionOptions
        {
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "test-key",
            Model = "test-model"
        });
        var service = new AgentExecutionService(httpClient, settings, secrets);
        return AgentExecutionTestSupport.CreateCoordinator(host, service, store);
    }

    [Fact]
    public async Task SendAsync_Success_ReturnsStructuredResultWithStableRunId()
    {
        var (host, panel, store) = CreateHostWithPanel();
        var handler = new SuccessHandler("Hello back");
        var coordinator = CreateCoordinator(host, store, handler);

        var result = await coordinator.SendAsync(panel.PanelId, "Hi");

        Assert.NotNull(result);
        Assert.NotEqual(default(ExecutionRunId), result!.Run.Id);
        Assert.Equal(ExecutionRunOutcome.Success, result.Run.Outcome);
        Assert.Equal(panel.ConversationId, result.Run.ConversationId);
        Assert.Equal(panel.ActorId, result.Run.TargetActorId);
        Assert.Equal(panel.PanelId, result.Run.TargetPanelId);
        Assert.Equal("Hello back", result.AssistantResponse);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_ExecutionFailure_ReturnsStructuredErrorResult()
    {
        var (host, panel, store) = CreateHostWithPanel();
        var handler = new StatusHandler(HttpStatusCode.InternalServerError, "Server error");
        var coordinator = CreateCoordinator(host, store, handler);

        var result = await coordinator.SendAsync(panel.PanelId, "Hello");

        Assert.NotNull(result);
        Assert.Equal(ExecutionRunOutcome.ExecutionFailure, result!.Run.Outcome);
        Assert.Null(result.AssistantResponse);
        Assert.Contains("500", result.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_CancelledServiceFailure_ReturnsCancelledOutcome()
    {
        var (host, panel, store) = CreateHostWithPanel();
        using var cts = new CancellationTokenSource();
        var handler = new DelayedSuccessHandler(TimeSpan.FromSeconds(5), "never");
        var coordinator = CreateCoordinator(host, store, handler);

        var sendTask = coordinator.SendAsync(panel.PanelId, "Hello", cts.Token);
        await Task.Delay(50);
        cts.Cancel();

        var result = await sendTask;

        Assert.NotNull(result);
        Assert.Equal(ExecutionRunOutcome.Cancelled, result!.Run.Outcome);
        Assert.Contains("cancel", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendAsync_ThrownException_ReturnsExecutionFailureOutcome()
    {
        var (host, panel, store) = CreateHostWithPanel();
        var handler = new FaultHandler(new HttpRequestException("connection refused"));
        var coordinator = CreateCoordinator(host, store, handler);

        var result = await coordinator.SendAsync(panel.PanelId, "Hello");

        Assert.NotNull(result);
        Assert.Equal(ExecutionRunOutcome.ExecutionFailure, result!.Run.Outcome);
        Assert.Contains("connection refused", result.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_DuplicateInFlight_ReturnsStructuredRejectionWithoutNewRun()
    {
        var (host, panel, store) = CreateHostWithPanel();
        var handler = new DelayedSuccessHandler(TimeSpan.FromMilliseconds(200), "Slow");
        var coordinator = CreateCoordinator(host, store, handler);

        var first = coordinator.SendAsync(panel.PanelId, "First");
        var second = await coordinator.SendAsync(panel.PanelId, "Second");

        Assert.NotNull(second);
        Assert.Equal(ExecutionRunOutcome.Rejected, second!.Run.Outcome);
        await first;
    }

    [Fact]
    public async Task RouteAndExecuteAsync_DirectSend_ResolvesSourceTypedTarget()
    {
        var (host, panel, store) = CreateHostWithPanel();
        var handler = new SuccessHandler("Direct reply");
        var coordinator = CreateCoordinator(host, store, handler);
        var router = new AgentRouter(new MentionParser(), host, coordinator, ConversationsTestSupport.CreateCatalog(), ConversationsTestSupport.CreateStore());

        var result = await router.RouteAndExecuteAsync(panel.PanelId, "hello");

        Assert.True(result.Success);
        Assert.NotNull(result.Request);
        Assert.True(result.Request!.IsDirectSend);
        Assert.Equal(panel.ActorId, result.Request.TargetActorId);
        Assert.Equal(panel.PanelId, result.Request.TargetPanelId);
        Assert.Equal(panel.ConversationId, result.Request.ConversationId);
        Assert.Null(result.Request.GetType().GetProperty("TargetAgentName"));
        Assert.NotNull(result.ExecutionResult);
        Assert.Equal(ExecutionRunOutcome.Success, result.ExecutionResult!.Run.Outcome);
    }

    [Fact]
    public async Task RouteAndExecuteAsync_RoutedSend_ResolvesTargetTypedIdentity()
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(catalog, store);
        var source = host.GetOrCreatePanelForActor(ActorId.PanelSeed("alpha"));
        var target = host.GetOrCreatePanelForActor(ActorId.PanelSeed("beta"));
        var handler = new SuccessHandler("Routed reply");
        var coordinator = CreateCoordinator(host, store, handler);
        var router = new AgentRouter(new MentionParser(), host, coordinator, catalog, store);

        var result = await router.RouteAndExecuteAsync(source.PanelId, "@Beta routed hello");

        Assert.True(result.Success);
        Assert.NotNull(result.Request);
        Assert.False(result.Request!.IsDirectSend);
        Assert.Equal(target.ActorId, result.Request.TargetActorId);
        Assert.Equal(target.PanelId, result.Request.TargetPanelId);
        Assert.Equal(target.ConversationId, result.Request.ConversationId);
        Assert.NotNull(result.ExecutionResult);
        Assert.Equal(target.PanelId, result.ExecutionResult!.Run.TargetPanelId);
        Assert.Equal(2, target.OutputHistory.Count);
        Assert.Empty(source.OutputHistory);
    }

    [Fact]
    public async Task RouteAndExecuteAsync_RoutingFailure_CreatesCorrelatedRoutingFailureRun()
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(catalog, store);
        var panel = host.GetOrCreatePanelForActor(ActorId.PanelSeed("alpha"));
        var handler = new SuccessHandler("unused");
        var coordinator = CreateCoordinator(host, store, handler);
        var router = new AgentRouter(new MentionParser(), host, coordinator, catalog, store);

        var result = await router.RouteAndExecuteAsync(panel.PanelId, "@Missing hello");

        Assert.False(result.Success);
        Assert.Equal("Unknown target", result.FailureReason);
        Assert.Null(result.Request);
        Assert.NotNull(result.ExecutionResult);
        Assert.Equal(ExecutionRunOutcome.RoutingFailure, result.ExecutionResult!.Run.Outcome);
        Assert.Equal(panel.ConversationId, result.ExecutionResult.Run.ConversationId);
        Assert.Equal(panel.ActorId, result.ExecutionResult.Run.TargetActorId);
        Assert.Equal(panel.PanelId, result.ExecutionResult.Run.TargetPanelId);
        Assert.Equal(ActorId.HumanUser, result.ExecutionResult.Run.InitiatingActorId);
        Assert.Equal("Unknown target", result.ExecutionResult.ErrorMessage);
        Assert.Null(result.ExecutionResult.AssistantResponse);
        // Routing failure is recorded as a typed entry and projected as Error: line.
        Assert.Single(panel.OutputHistory);
        Assert.StartsWith("Error:", panel.OutputHistory[0]);
    }

    [Fact]
    public async Task RouteAndExecuteAsync_AmbiguousTarget_CreatesCorrelatedRoutingFailureRun()
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(catalog, store);
        var source = host.GetOrCreatePanelForActor(ActorId.PanelSeed("alpha"));
        host.CreatePanel("agent-twin-a", "Twin", "avatar_a");
        host.CreatePanel("agent-twin-b", "Twin", "avatar_b");
        var handler = new SuccessHandler("unused");
        var coordinator = CreateCoordinator(host, store, handler);
        var router = new AgentRouter(new MentionParser(), host, coordinator, catalog, store);

        var result = await router.RouteAndExecuteAsync(source.PanelId, "@Twin hello");

        Assert.False(result.Success);
        Assert.Equal("Ambiguous target", result.FailureReason);
        Assert.NotNull(result.ExecutionResult);
        Assert.Equal(ExecutionRunOutcome.RoutingFailure, result.ExecutionResult!.Run.Outcome);
        Assert.Equal(source.ActorId, result.ExecutionResult.Run.TargetActorId);
        Assert.Equal("Ambiguous target", result.ExecutionResult.ErrorMessage);
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
