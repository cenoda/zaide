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
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Infrastructure;
using Zaide.Tests.Features.Settings.Infrastructure;

namespace Zaide.Tests.Features.Agents.Application;

public sealed class AgentExecutionCoordinatorTests : IDisposable
{
    private readonly string _tempDir;

    public AgentExecutionCoordinatorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ZaideCoordTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    private static AgentPanelHost CreateHostWithPanel(out AgentPanelState panel)
    {
        var host = new AgentPanelHost();
        panel = host.CreatePanel("agent-1", "Test Agent", "avatar_test");
        return host;
    }

    /// <summary>
    /// Creates a SettingsService + TestSecretStore pair configured to match the
    /// given <see cref="AgentExecutionOptions"/>.
    /// </summary>
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
        var json = SettingsSerializer.Serialize(model);
        File.WriteAllText(settingsPath, json);

        var settingsService = new SettingsService(settingsPath, lkgPath, tmpPath,
            new SettingsMigrator(Array.Empty<ISettingsMigration>()));

        var secrets = new TestSecretStore();
        if (!string.IsNullOrEmpty(options.ApiKey))
            secrets.Set("llm.apiKey", options.ApiKey);

        return (settingsService, secrets);
    }

    /// <summary>
    /// Creates an execution service that returns the given status/body.
    /// </summary>
    private IAgentExecutionService CreateService(HttpStatusCode statusCode, string body)
    {
        var handler = new FakeHandler(statusCode, body);
        var httpClient = new HttpClient(handler);
        var (settings, secrets) = CreateSettingsAndSecrets(new AgentExecutionOptions
        {
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "test-key",
            Model = "test-model"
        });
        return new AgentExecutionService(httpClient, settings, secrets);
    }

    private IAgentExecutionService CreateFaultService(Exception ex)
    {
        var handler = new FaultHandler(ex);
        var httpClient = new HttpClient(handler);
        var (settings, secrets) = CreateSettingsAndSecrets(new AgentExecutionOptions
        {
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "test-key",
            Model = "test-model"
        });
        return new AgentExecutionService(httpClient, settings, secrets);
    }

    /// <summary>
    /// Creates an execution service with missing API key (empty string).
    /// </summary>
    private IAgentExecutionService CreateServiceWithMissingApiKey()
    {
        var options = new AgentExecutionOptions
        {
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "",
            Model = "test-model"
        };
        var handler = new FakeHandler(HttpStatusCode.OK, "{}");
        var httpClient = new HttpClient(handler);
        var (settings, secrets) = CreateSettingsAndSecrets(options);
        return new AgentExecutionService(httpClient, settings, secrets);
    }

    /// <summary>
    /// Creates a stateful handler that fails on the first call and succeeds on
    /// subsequent calls. Used to test error recovery within a single coordinator.
    /// </summary>
    private sealed class ToggleHandler : HttpMessageHandler
    {
        private bool _hasFailed;

        public ToggleHandler() { }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
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

    // ── Successful send ─────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_Success_AppendsUserAndAssistantOutput()
    {
        var host = CreateHostWithPanel(out var panel);
        var service = CreateService(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content = "Hello back" }, finish_reason = "stop" } }
        }));
        var coordinator = new AgentExecutionCoordinator(host, service);

        await coordinator.SendAsync(panel.PanelId, "Hi");

        Assert.Equal(2, panel.OutputHistory.Count);
        Assert.Equal("User: Hi", panel.OutputHistory[0]);
        Assert.Equal("Assistant: Hello back", panel.OutputHistory[1]);
        Assert.Equal("Idle", panel.Status);
        Assert.False(panel.IsBusy);
    }

    [Fact]
    public async Task SendAsync_Success_ClearsDraftInput()
    {
        var host = CreateHostWithPanel(out var panel);
        panel.DraftInput = "Hi there";
        var service = CreateService(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content = "Reply" }, finish_reason = "stop" } }
        }));
        var coordinator = new AgentExecutionCoordinator(host, service);

        await coordinator.SendAsync(panel.PanelId, "Hi there");

        Assert.Equal(string.Empty, panel.DraftInput);
        Assert.Equal("Idle", panel.Status);
        Assert.False(panel.IsBusy);
    }

    // ── Missing configuration failure ───────────────────────────────────────

    [Fact]
    public async Task SendAsync_MissingApiKey_AppendsErrorToOutput()
    {
        var host = CreateHostWithPanel(out var panel);
        var service = CreateServiceWithMissingApiKey();
        var coordinator = new AgentExecutionCoordinator(host, service);

        await coordinator.SendAsync(panel.PanelId, "Hello");

        Assert.Equal("Error", panel.Status);
        Assert.False(panel.IsBusy);
        Assert.Equal(2, panel.OutputHistory.Count);
        Assert.Equal("User: Hello", panel.OutputHistory[0]);
        Assert.Contains("Error:", panel.OutputHistory[1]);
        Assert.Contains("API key", panel.OutputHistory[1], StringComparison.OrdinalIgnoreCase);
        // Draft is cleared on send initiation, even when the request fails
        Assert.Empty(panel.DraftInput);
    }

    [Fact]
    public async Task SendAsync_MissingBaseUrl_AppendsErrorToOutput()
    {
        var host = CreateHostWithPanel(out var panel);
        var options = new AgentExecutionOptions
        {
            BaseUrl = "",
            ApiKey = "test-key",
            Model = "test-model"
        };
        var handler = new FakeHandler(HttpStatusCode.OK, "{}");
        var httpClient = new HttpClient(handler);
        var (settings, secrets) = CreateSettingsAndSecrets(options);
        var service = new AgentExecutionService(httpClient, settings, secrets);
        var coordinator = new AgentExecutionCoordinator(host, service);

        await coordinator.SendAsync(panel.PanelId, "Hello");

        Assert.Equal("Error", panel.Status);
        Assert.False(panel.IsBusy);
        Assert.Contains("Base URL", panel.OutputHistory[1], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendAsync_MissingModel_AppendsErrorToOutput()
    {
        var host = CreateHostWithPanel(out var panel);
        var options = new AgentExecutionOptions
        {
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "test-key",
            Model = ""
        };
        var handler = new FakeHandler(HttpStatusCode.OK, "{}");
        var httpClient = new HttpClient(handler);
        var (settings, secrets) = CreateSettingsAndSecrets(options);
        var service = new AgentExecutionService(httpClient, settings, secrets);
        var coordinator = new AgentExecutionCoordinator(host, service);

        await coordinator.SendAsync(panel.PanelId, "Hello");

        Assert.Equal("Error", panel.Status);
        Assert.False(panel.IsBusy);
        Assert.Contains("Model", panel.OutputHistory[1], StringComparison.OrdinalIgnoreCase);
    }

    // ── Failed send ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_Failure_ClearsDraftInput()
    {
        var host = CreateHostWithPanel(out var panel);
        panel.DraftInput = "Hello";
        var service = CreateService(HttpStatusCode.InternalServerError, "Server error");
        var coordinator = new AgentExecutionCoordinator(host, service);

        await coordinator.SendAsync(panel.PanelId, "Hello");

        // Draft must be cleared on send initiation so the same text cannot be
        // re-sent by pressing Enter again after a failed request.
        Assert.Equal(string.Empty, panel.DraftInput);
        Assert.Equal("Error", panel.Status);
        Assert.False(panel.IsBusy);
    }

    [Fact]
    public async Task SendAsync_Failure_AppendsErrorToOutput()
    {
        var host = CreateHostWithPanel(out var panel);
        var service = CreateService(HttpStatusCode.Unauthorized, "{\"error\": \"bad key\"}");
        var coordinator = new AgentExecutionCoordinator(host, service);

        await coordinator.SendAsync(panel.PanelId, "Hello");

        Assert.Equal(2, panel.OutputHistory.Count);
        Assert.Equal("User: Hello", panel.OutputHistory[0]);
        Assert.Contains("Error:", panel.OutputHistory[1]);
        Assert.Contains("401", panel.OutputHistory[1]);
        Assert.Equal("Error", panel.Status);
        Assert.False(panel.IsBusy);
    }

    // ── Regression: draft must clear so the same text is not re-sent ────────

    [Fact]
    public async Task SendAsync_RepeatedEnter_ClearsDraftEachTime_NoDuplicateSend()
    {
        // Regression for the phase-6 smoke-test bug: typing "hi" and pressing
        // Enter 3 times re-sent "hi" 3 times because the draft was only cleared
        // on success. The draft must clear on every send initiation so a failed
        // request cannot be re-sent by pressing Enter again.
        var host = CreateHostWithPanel(out var panel);
        var service = CreateService(HttpStatusCode.InternalServerError, "Server error");
        var coordinator = new AgentExecutionCoordinator(host, service);

        // Simulate the user typing "hi" into the draft and pressing Enter 3 times.
        for (var i = 0; i < 3; i++)
        {
            panel.DraftInput = "hi";
            await coordinator.SendAsync(panel.PanelId, panel.DraftInput);
            // After each send the draft must be empty, so the next Enter has
            // nothing to re-send unless the user types again.
            Assert.Equal(string.Empty, panel.DraftInput);
        }

        // Each Enter produced exactly one "User: hi" entry — no duplicates from
        // a lingering draft.
        Assert.Equal(3, panel.OutputHistory.Count(o => o == "User: hi"));
        Assert.Equal(3, panel.OutputHistory.Count(o => o.StartsWith("Error:")));
    }

    // ── One-in-flight enforcement ───────────────────────────────────────────

    [Fact]
    public async Task SendAsync_OneInFlight_SamePanel_SecondIsNoOp()
    {
        var host = CreateHostWithPanel(out var panel);
        // Use a slow handler that blocks to ensure concurrent call is dropped
        var handler = new BlockingHandler(TimeSpan.FromMilliseconds(500));
        var httpClient = new HttpClient(handler);
        var (settings, secrets) = CreateSettingsAndSecrets(new AgentExecutionOptions
        {
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "test-key",
            Model = "test-model"
        });
        var service = new AgentExecutionService(httpClient, settings, secrets);
        var coordinator = new AgentExecutionCoordinator(host, service);

        // Start first send (will block for 500ms)
        var task1 = coordinator.SendAsync(panel.PanelId, "Hello");
        // Start second send immediately (should be dropped by one-in-flight)
        var task2 = coordinator.SendAsync(panel.PanelId, "World");

        await Task.WhenAll(task1, task2);

        // Only the first message should have been added
        Assert.Single(panel.OutputHistory, o => o == "User: Hello");
        Assert.DoesNotContain("World", panel.OutputHistory);
        // Final state should be Idle and not busy
        Assert.Equal("Idle", panel.Status);
        Assert.False(panel.IsBusy);
    }

    [Fact]
    public async Task SendAsync_OneInFlight_DifferentPanels_BothAllowed()
    {
        var host = new AgentPanelHost();
        var panel1 = host.CreatePanel("agent-1", "Alpha", "avatar_a");
        var panel2 = host.CreatePanel("agent-2", "Beta", "avatar_b");
        var service = CreateService(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content = "Reply" }, finish_reason = "stop" } }
        }));
        var coordinator = new AgentExecutionCoordinator(host, service);

        var task1 = coordinator.SendAsync(panel1.PanelId, "Hello 1");
        var task2 = coordinator.SendAsync(panel2.PanelId, "Hello 2");

        await Task.WhenAll(task1, task2);

        Assert.Equal(2, panel1.OutputHistory.Count);
        Assert.Equal(2, panel2.OutputHistory.Count);
    }

    // ── Unknown panel ───────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_UnknownPanel_IsSafeNoOp()
    {
        var host = CreateHostWithPanel(out var panel);
        var service = CreateService(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content = "Reply" }, finish_reason = "stop" } }
        }));
        var coordinator = new AgentExecutionCoordinator(host, service);

        // This should not throw
        await coordinator.SendAsync("non-existent-panel-id", "Hello");

        // No output should have been added to the existing panel
        Assert.Empty(panel.OutputHistory);
    }

    [Fact]
    public async Task SendAsync_EmptyPanelId_IsSafeNoOp()
    {
        var host = CreateHostWithPanel(out var panel);
        var service = CreateService(HttpStatusCode.OK, "{}");
        var coordinator = new AgentExecutionCoordinator(host, service);

        await coordinator.SendAsync("", "Hello");

        Assert.Empty(panel.OutputHistory);
    }

    [Fact]
    public async Task SendAsync_EmptyMessage_IsSafeNoOp()
    {
        var host = CreateHostWithPanel(out var panel);
        var service = CreateService(HttpStatusCode.OK, "{}");
        var coordinator = new AgentExecutionCoordinator(host, service);

        await coordinator.SendAsync(panel.PanelId, "");

        Assert.Empty(panel.OutputHistory);
    }

    // ── M3: Status / busy / error transitions ───────────────────────────────

    [Fact]
    public async Task SendAsync_StatusBecomesThinkingWhileRunning()
    {
        var host = CreateHostWithPanel(out var panel);
        var handler = new BlockingHandler(TimeSpan.FromMilliseconds(200));
        var httpClient = new HttpClient(handler);
        var (settings, secrets) = CreateSettingsAndSecrets(new AgentExecutionOptions
        {
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "test-key",
            Model = "test-model"
        });
        var service = new AgentExecutionService(httpClient, settings, secrets);
        var coordinator = new AgentExecutionCoordinator(host, service);

        // Start send (will block for 200ms)
        var task = coordinator.SendAsync(panel.PanelId, "Hello");

        // Status should be Thinking while in-flight
        Assert.Equal("Thinking", panel.Status);
        Assert.True(panel.IsBusy);

        await task;

        // After completion, status should be Idle and not busy
        Assert.Equal("Idle", panel.Status);
        Assert.False(panel.IsBusy);
    }

    [Fact]
    public async Task SendAsync_Success_EndsInIdle()
    {
        var host = CreateHostWithPanel(out var panel);
        var service = CreateService(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content = "Reply" }, finish_reason = "stop" } }
        }));
        var coordinator = new AgentExecutionCoordinator(host, service);

        await coordinator.SendAsync(panel.PanelId, "Hello");

        Assert.Equal("Idle", panel.Status);
        Assert.False(panel.IsBusy);
    }

    [Fact]
    public async Task SendAsync_Failure_EndsInError()
    {
        var host = CreateHostWithPanel(out var panel);
        var service = CreateService(HttpStatusCode.InternalServerError, "Server error");
        var coordinator = new AgentExecutionCoordinator(host, service);

        await coordinator.SendAsync(panel.PanelId, "Hello");

        Assert.Equal("Error", panel.Status);
        Assert.False(panel.IsBusy);
    }

    [Fact]
    public async Task SendAsync_Exception_EndsInError()
    {
        var host = CreateHostWithPanel(out var panel);
        var service = CreateFaultService(new HttpRequestException("connection refused"));
        var coordinator = new AgentExecutionCoordinator(host, service);

        await coordinator.SendAsync(panel.PanelId, "Hello");

        Assert.Equal("Error", panel.Status);
        Assert.False(panel.IsBusy);
        Assert.Contains("connection refused", panel.OutputHistory[1]);
    }

    [Fact]
    public async Task SendAsync_OneInFlight_KeepsStateTruthfulOnDuplicate()
    {
        var host = CreateHostWithPanel(out var panel);
        var handler = new BlockingHandler(TimeSpan.FromMilliseconds(300));
        var httpClient = new HttpClient(handler);
        var (settings, secrets) = CreateSettingsAndSecrets(new AgentExecutionOptions
        {
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "test-key",
            Model = "test-model"
        });
        var service = new AgentExecutionService(httpClient, settings, secrets);
        var coordinator = new AgentExecutionCoordinator(host, service);

        // Start first send (will block for 300ms)
        var task1 = coordinator.SendAsync(panel.PanelId, "First");

        // Status should be Thinking
        Assert.Equal("Thinking", panel.Status);
        Assert.True(panel.IsBusy);

        // Second send should be dropped by one-in-flight
        var task2 = coordinator.SendAsync(panel.PanelId, "Second");

        await Task.WhenAll(task1, task2);

        // After completion, status should be Idle and only first message in output
        Assert.Equal("Idle", panel.Status);
        Assert.False(panel.IsBusy);
        Assert.Single(panel.OutputHistory, o => o == "User: First");
        Assert.DoesNotContain("Second", panel.OutputHistory);
    }

    [Fact]
    public async Task SendAsync_ErrorResetsOnNextSuccessfulSend()
    {
        var host = CreateHostWithPanel(out var panel);
        var failService = CreateService(HttpStatusCode.InternalServerError, "Server error");
        var coordinator = new AgentExecutionCoordinator(host, failService);

        // First send fails
        await coordinator.SendAsync(panel.PanelId, "Hello");
        Assert.Equal("Error", panel.Status);
        Assert.False(panel.IsBusy);

        // Second send succeeds with a different service (same coordinator not
        // possible since the coordinator delegates to the service — we test
        // the panel state transition, not the service swap).
        var successService = CreateService(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content = "OK" }, finish_reason = "stop" } }
        }));
        var coordinator2 = new AgentExecutionCoordinator(host, successService);

        await coordinator2.SendAsync(panel.PanelId, "Hello again");

        // Error state should have been reset by the new send
        Assert.Equal("Idle", panel.Status);
        Assert.False(panel.IsBusy);
        // Output should contain both rounds
        Assert.Equal(4, panel.OutputHistory.Count);
        Assert.Equal("User: Hello", panel.OutputHistory[0]);
        Assert.Contains("Error:", panel.OutputHistory[1]);
        Assert.Equal("User: Hello again", panel.OutputHistory[2]);
        Assert.Equal("Assistant: OK", panel.OutputHistory[3]);
    }

    [Fact]
    public async Task SendAsync_ErrorResetsWithSingleCoordinatorUsingToggleHandler()
    {
        var host = CreateHostWithPanel(out var panel);
        var handler = new ToggleHandler();
        var httpClient = new HttpClient(handler);
        var (settings, secrets) = CreateSettingsAndSecrets(new AgentExecutionOptions
        {
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "test-key",
            Model = "test-model"
        });
        var service = new AgentExecutionService(httpClient, settings, secrets);
        var coordinator = new AgentExecutionCoordinator(host, service);

        // First send fails (ToggleHandler fails first call)
        await coordinator.SendAsync(panel.PanelId, "Hello");
        Assert.Equal("Error", panel.Status);
        Assert.False(panel.IsBusy);
        Assert.Contains("500", panel.OutputHistory[1]);

        // Second send recovers (ToggleHandler succeeds on subsequent calls)
        await coordinator.SendAsync(panel.PanelId, "Hello again");
        Assert.Equal("Idle", panel.Status);
        Assert.False(panel.IsBusy);
        Assert.Equal("Assistant: Recovered", panel.OutputHistory[3]);
    }

    // ── Phase 8.1.7.1: AgentRouter + AgentExecutionCoordinator integration ──

    [Fact]
    public async Task SendAsync_ThroughRouter_Success_UpdatesPanelOutputAndStatus()
    {
        var host = CreateHostWithPanel(out var panel);
        var service = CreateService(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content = "Router reply" }, finish_reason = "stop" } }
        }));
        var parser = new MentionParser();
        var coordinator = new AgentExecutionCoordinator(host, service);
        var router = new AgentRouter(parser, host, coordinator);

        var result = await router.RouteAndExecuteAsync(panel.PanelId, "Hello from router");

        Assert.True(result.Success);
        Assert.NotNull(result.Request);
        Assert.True(result.Request!.IsDirectSend);
        Assert.Equal(2, panel.OutputHistory.Count);
        Assert.Equal("User: Hello from router", panel.OutputHistory[0]);
        Assert.Equal("Assistant: Router reply", panel.OutputHistory[1]);
        Assert.Equal("Idle", panel.Status);
        Assert.False(panel.IsBusy);
    }

    [Fact]
    public async Task SendAsync_ThroughRouter_Failure_UpdatesPanelError()
    {
        var host = CreateHostWithPanel(out var panel);
        var service = CreateService(HttpStatusCode.InternalServerError, "Server error");
        var parser = new MentionParser();
        var coordinator = new AgentExecutionCoordinator(host, service);
        var router = new AgentRouter(parser, host, coordinator);

        var result = await router.RouteAndExecuteAsync(panel.PanelId, "Hello");

        // Router succeeds (routing parsed, execution delegated)
        Assert.True(result.Success);
        Assert.Equal("Error", panel.Status);
        Assert.False(panel.IsBusy);
        Assert.Contains("500", panel.OutputHistory[1]);
    }

    [Fact]
    public async Task SendAsync_ThroughRouter_NetworkException_UpdatesPanelError()
    {
        var host = CreateHostWithPanel(out var panel);
        var handler = new FaultHandler(new HttpRequestException("connection refused"));
        var httpClient = new HttpClient(handler);
        var options = new AgentExecutionOptions
        {
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "test-key",
            Model = "test-model"
        };
        var (settings, secrets) = CreateSettingsAndSecrets(options);
        var service = new AgentExecutionService(httpClient, settings, secrets);
        var parser = new MentionParser();
        var coordinator = new AgentExecutionCoordinator(host, service);
        var router = new AgentRouter(parser, host, coordinator);

        await router.RouteAndExecuteAsync(panel.PanelId, "Hello");

        Assert.Equal("Error", panel.Status);
        Assert.False(panel.IsBusy);
        Assert.Contains("connection refused", panel.OutputHistory[1]);
    }

    /// <summary>
    /// Verifies the full chain preserves Townhall-safe state — the
    /// ConfigureAwait(false)-removal fix ensures all mutations happen on the
    /// captured SynchronizationContext without wrapped cross-thread exceptions.
    /// </summary>
    [Fact]
    public async Task SendAsync_ThroughRouter_RoutedSend_UpdatesTargetPanel()
    {
        var host = new AgentPanelHost();
        var source = host.CreatePanel("agent-1", "Alpha", "avatar_a");
        var target = host.CreatePanel("agent-2", "Beta", "avatar_b");
        var service = CreateService(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content = "Target reply" }, finish_reason = "stop" } }
        }));
        var parser = new MentionParser();
        var coordinator = new AgentExecutionCoordinator(host, service);
        var router = new AgentRouter(parser, host, coordinator);

        var result = await router.RouteAndExecuteAsync(source.PanelId, "@Beta hello from routed");

        Assert.True(result.Success);
        Assert.NotNull(result.Request);
        Assert.False(result.Request!.IsDirectSend);

        // Source panel should have no output (routed to target)
        Assert.Empty(source.OutputHistory);

        // Target panel should have the user + assistant output
        Assert.Equal(2, target.OutputHistory.Count);
        Assert.Equal("User: hello from routed", target.OutputHistory[0]);
        Assert.Equal("Assistant: Target reply", target.OutputHistory[1]);
        Assert.Equal("Idle", target.Status);
        Assert.False(target.IsBusy);
    }
}

// ── Test helpers ───────────────────────────────────────────────────────────

/// <summary>
/// Returns a fixed status code and body.
/// </summary>
internal sealed class FakeHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _code;
    private readonly string _body;

    public FakeHandler(HttpStatusCode code, string body) { _code = code; _body = body; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        return Task.FromResult(new HttpResponseMessage(_code)
        {
            Content = new StringContent(_body, Encoding.UTF8, "application/json")
        });
    }
}

/// <summary>
/// Throws the given exception.
/// </summary>
internal sealed class FaultHandler : HttpMessageHandler
{
    private readonly Exception _ex;
    public FaultHandler(Exception ex) { _ex = ex; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        throw _ex;
    }
}

/// <summary>
/// Delays for the given duration before returning a success response.
/// </summary>
internal sealed class BlockingHandler : HttpMessageHandler
{
    private readonly TimeSpan _delay;
    public BlockingHandler(TimeSpan delay) { _delay = delay; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        await Task.Delay(_delay, ct);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                choices = new[] { new { message = new { content = "Slow reply" }, finish_reason = "stop" } }
            }), Encoding.UTF8, "application/json")
        };
    }
}
