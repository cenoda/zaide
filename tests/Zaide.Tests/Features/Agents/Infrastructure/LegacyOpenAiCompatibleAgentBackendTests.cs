using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Zaide.App.Composition.Registration;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Infrastructure;
using Zaide.Tests.Features.Settings.Infrastructure;

namespace Zaide.Tests.Features.Agents.Infrastructure;

public sealed class LegacyOpenAiCompatibleAgentBackendTests : IDisposable
{
    internal const string SentinelSecret = AgentExecutionServiceTests.SentinelSecret;

    private static readonly AgentBackendId ExpectedBackendId =
        AgentBackendId.FromValue(LegacyOpenAiCompatibleAgentBackend.BackendIdValue);

    private readonly string _tempDir;
    private readonly Dictionary<string, string?> _originalEnvironment;

    public LegacyOpenAiCompatibleAgentBackendTests()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            "ZaideLegacyOpenAiBackendTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _originalEnvironment = CaptureEnvironment(
            "AGENT_API_URL",
            "AGENT_MODEL",
            "AGENT_API_KEY");
    }

    public void Dispose()
    {
        RestoreEnvironment(_originalEnvironment);
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    // ── Identity ────────────────────────────────────────────────────────────

    [Fact]
    public void Backend_ExposesStableIdAndVersion()
    {
        var backend = CreateBackend(CreateService(HttpStatusCode.OK, SuccessBody("ok")));

        Assert.Equal(ExpectedBackendId, backend.BackendId);
        Assert.Equal(LegacyOpenAiCompatibleAgentBackend.BackendVersionValue, backend.BackendVersion);
    }

    // ── Capability snapshots ────────────────────────────────────────────────

    [Fact]
    public void CapabilitySnapshot_WhenConfigured_ReportsMessageCompletionUsable()
    {
        var backend = CreateBackend(CreateService(HttpStatusCode.OK, SuccessBody("ok")));

        var snapshot = backend.CapabilitySnapshot;

        Assert.Equal(ExpectedBackendId, snapshot.BackendId);
        Assert.Equal(1, snapshot.Version);
        Assert.True(snapshot.TryGetState(AgentCapabilityId.MessageCompletion, out var completion));
        Assert.Equal(AgentCapabilityFactValue.Supported, completion.Advertised);
        Assert.Equal(AgentCapabilityFactValue.Supported, completion.Available);
        Assert.Equal(AgentCapabilityFactValue.Supported, completion.Configured);
        Assert.Equal(AgentCapabilityFactValue.Supported, completion.CurrentlyUsable);

        Assert.True(snapshot.TryGetState(AgentCapabilityId.Streaming, out var streaming));
        Assert.Equal(AgentCapabilityFactValue.NotSupported, streaming.Available);
        Assert.Equal(AgentCapabilityFactValue.NotSupported, streaming.CurrentlyUsable);

        Assert.True(snapshot.TryGetState(AgentCapabilityId.Tools, out var tools));
        Assert.Equal(AgentCapabilityFactValue.Unavailable, tools.Advertised);
        Assert.Equal(AgentCapabilityFactValue.Unavailable, tools.CurrentlyUsable);

        Assert.True(snapshot.TryGetState(AgentCapabilityId.Attachments, out var attachments));
        Assert.Equal(AgentCapabilityFactValue.Unavailable, attachments.Advertised);
        Assert.Equal(AgentCapabilityFactValue.Unavailable, attachments.Available);
        Assert.Equal(AgentCapabilityFactValue.Unavailable, attachments.CurrentlyUsable);
    }

    [Fact]
    public void CapabilitySnapshot_Unconfigured_StartsAtVersionOne()
    {
        var options = new AgentExecutionOptions
        {
            BaseUrl = "https://api.test.com/v1",
            ApiKey = string.Empty,
            Model = "test-model",
        };
        var backend = CreateBackend(CreateService(HttpStatusCode.OK, SuccessBody("ok"), options));

        var snapshot = backend.CapabilitySnapshot;

        Assert.Equal(1, snapshot.Version);
        Assert.True(snapshot.TryGetState(AgentCapabilityId.MessageCompletion, out var completion));
        Assert.Equal(AgentCapabilityFactValue.Unavailable, completion.CurrentlyUsable);
    }

    [Fact]
    public void CapabilitySnapshot_TransitionToConfigured_IncrementsVersion()
    {
        var options = new AgentExecutionOptions
        {
            BaseUrl = "https://api.test.com/v1",
            ApiKey = string.Empty,
            Model = "test-model",
        };
        var handler = new CaptureMessageHandler(_ => Ok(SuccessBody("ok")));
        var httpClient = new HttpClient(handler);
        var (settings, secrets) = CreateSettingsAndSecrets(options);
        var executionService = new AgentExecutionService(httpClient, settings, secrets);
        var backend = new LegacyOpenAiCompatibleAgentBackend(executionService);

        var initial = backend.CapabilitySnapshot;
        Assert.Equal(1, initial.Version);
        Assert.True(initial.TryGetState(AgentCapabilityId.MessageCompletion, out var initialCompletion));
        Assert.Equal(AgentCapabilityFactValue.Unavailable, initialCompletion.CurrentlyUsable);

        secrets.Set("llm.apiKey", "test-key-123");

        var updated = backend.CapabilitySnapshot;
        Assert.Equal(2, updated.Version);
        Assert.True(updated.TryGetState(AgentCapabilityId.MessageCompletion, out var configuredCompletion));
        Assert.Equal(AgentCapabilityFactValue.Supported, configuredCompletion.CurrentlyUsable);
    }

    [Fact]
    public void CapabilitySnapshot_FirstReadFailureThenConfigured_IncrementsVersion()
    {
        var (innerSettings, secrets) = CreateSettingsAndSecrets(DefaultOptions());
        var throwingSettings = new ThrowOnFirstCurrentReadSettingsService(innerSettings);
        var executionService = new AgentExecutionService(
            new HttpClient(new CaptureMessageHandler(_ => Ok(SuccessBody("ok")))),
            throwingSettings,
            secrets);
        var backend = new LegacyOpenAiCompatibleAgentBackend(executionService);

        var afterFailure = backend.CapabilitySnapshot;
        Assert.Equal(1, afterFailure.Version);
        Assert.True(afterFailure.TryGetState(AgentCapabilityId.MessageCompletion, out var unavailable));
        Assert.Equal(AgentCapabilityFactValue.Unavailable, unavailable.CurrentlyUsable);

        var afterSuccess = backend.CapabilitySnapshot;
        Assert.Equal(2, afterSuccess.Version);
        Assert.True(afterSuccess.TryGetState(AgentCapabilityId.MessageCompletion, out var configured));
        Assert.Equal(AgentCapabilityFactValue.Supported, configured.CurrentlyUsable);
    }

    [Fact]
    public void CapabilitySnapshot_ConfiguredThenResolutionFailure_BecomesUnavailableAndIncrementsVersion()
    {
        var (innerSettings, secrets) = CreateSettingsAndSecrets(DefaultOptions());
        var settings = new ControllableCurrentReadSettingsService(innerSettings);
        var executionService = new AgentExecutionService(
            new HttpClient(new CaptureMessageHandler(_ => Ok(SuccessBody("ok")))),
            settings,
            secrets);
        var backend = new LegacyOpenAiCompatibleAgentBackend(executionService);

        var configured = backend.CapabilitySnapshot;
        Assert.Equal(1, configured.Version);
        Assert.True(configured.TryGetState(AgentCapabilityId.MessageCompletion, out var supported));
        Assert.Equal(AgentCapabilityFactValue.Supported, supported.CurrentlyUsable);

        settings.SetCurrentReadFails(true);

        var unavailable = backend.CapabilitySnapshot;
        Assert.Equal(2, unavailable.Version);
        Assert.True(unavailable.TryGetState(AgentCapabilityId.MessageCompletion, out var degraded));
        Assert.Equal(AgentCapabilityFactValue.Unavailable, degraded.CurrentlyUsable);
        Assert.Equal(AgentCapabilityFactValue.Unknown, degraded.Configured);
    }

    [Fact]
    public void CapabilitySnapshot_RepeatedResolutionFailure_DoesNotBumpVersionAgain()
    {
        var (innerSettings, secrets) = CreateSettingsAndSecrets(DefaultOptions());
        var settings = new ControllableCurrentReadSettingsService(innerSettings);
        var executionService = new AgentExecutionService(
            new HttpClient(new CaptureMessageHandler(_ => Ok(SuccessBody("ok")))),
            settings,
            secrets);
        var backend = new LegacyOpenAiCompatibleAgentBackend(executionService);

        _ = backend.CapabilitySnapshot;
        settings.SetCurrentReadFails(true);

        var firstFailure = backend.CapabilitySnapshot;
        var secondFailure = backend.CapabilitySnapshot;

        Assert.Equal(2, firstFailure.Version);
        Assert.Equal(firstFailure.Version, secondFailure.Version);
        Assert.True(secondFailure.TryGetState(AgentCapabilityId.MessageCompletion, out var completion));
        Assert.Equal(AgentCapabilityFactValue.Unavailable, completion.CurrentlyUsable);
    }

    [Fact]
    public void CapabilitySnapshot_ResolutionRecovery_ReturnsConfiguredAndIncrementsVersion()
    {
        var (innerSettings, secrets) = CreateSettingsAndSecrets(DefaultOptions());
        var settings = new ControllableCurrentReadSettingsService(innerSettings);
        var executionService = new AgentExecutionService(
            new HttpClient(new CaptureMessageHandler(_ => Ok(SuccessBody("ok")))),
            settings,
            secrets);
        var backend = new LegacyOpenAiCompatibleAgentBackend(executionService);

        _ = backend.CapabilitySnapshot;
        settings.SetCurrentReadFails(true);
        _ = backend.CapabilitySnapshot;

        settings.SetCurrentReadFails(false);

        var recovered = backend.CapabilitySnapshot;
        Assert.Equal(3, recovered.Version);
        Assert.True(recovered.TryGetState(AgentCapabilityId.MessageCompletion, out var completion));
        Assert.Equal(AgentCapabilityFactValue.Supported, completion.CurrentlyUsable);
        Assert.Equal(AgentCapabilityFactValue.Supported, completion.Configured);
    }

    [Fact]
    public void CapabilitySnapshot_UnchangedRead_ReturnsSameVersion()
    {
        var backend = CreateBackend(CreateService(HttpStatusCode.OK, SuccessBody("ok")));

        var first = backend.CapabilitySnapshot;
        var second = backend.CapabilitySnapshot;

        Assert.Equal(1, first.Version);
        Assert.Equal(first.Version, second.Version);
    }

    [Fact]
    public void CapabilitySnapshot_WhenUnconfigured_ReportsMessageCompletionUnavailable()
    {
        var options = new AgentExecutionOptions
        {
            BaseUrl = "https://api.test.com/v1",
            ApiKey = string.Empty,
            Model = "test-model",
        };
        var backend = CreateBackend(CreateService(HttpStatusCode.OK, SuccessBody("ok"), options));

        var snapshot = backend.CapabilitySnapshot;

        Assert.True(snapshot.TryGetState(AgentCapabilityId.MessageCompletion, out var completion));
        Assert.Equal(AgentCapabilityFactValue.Supported, completion.Advertised);
        Assert.Equal(AgentCapabilityFactValue.Unavailable, completion.Available);
        Assert.Equal(AgentCapabilityFactValue.Unavailable, completion.Configured);
        Assert.Equal(AgentCapabilityFactValue.Unavailable, completion.CurrentlyUsable);
    }

    // ── Live configuration precedence ───────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_EnvironmentOverridesSettings_OnEachExecution()
    {
        ClearEnvironment("AGENT_API_URL", "AGENT_MODEL", "AGENT_API_KEY");

        var handler = new CaptureMessageHandler(_ => Ok(SuccessBody("ok")));
        var httpClient = new HttpClient(handler);
        var (settings, secrets) = CreateSettingsAndSecrets(new AgentExecutionOptions
        {
            BaseUrl = "https://settings.test/v1",
            ApiKey = "settings-key",
            Model = "settings-model",
        });
        var executionService = new AgentExecutionService(httpClient, settings, secrets);
        var backend = new LegacyOpenAiCompatibleAgentBackend(executionService);

        SetEnvironment("AGENT_API_URL", "https://env.test/v1");
        SetEnvironment("AGENT_MODEL", "env-model");
        SetEnvironment("AGENT_API_KEY", "env-key");

        await CollectSingleEventAsync(backend, "hello");

        var captured = handler.LastRequest
            ?? throw new InvalidOperationException("Expected one HTTP request.");
        Assert.Equal("https://env.test/v1/chat/completions", captured.RequestUri?.ToString());
        Assert.Equal("Bearer env-key", captured.Headers.Authorization?.ToString());

        using var doc = JsonDocument.Parse(await ReadBodyAsync(captured));
        Assert.Equal("env-model", doc.RootElement.GetProperty("model").GetString());
    }

    // ── Request shape ───────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_SubmitsSingleUserMessage_WithStreamFalseAndNoExtraFields()
    {
        var handler = new CaptureMessageHandler(_ => Ok(SuccessBody("Assistant reply")));
        var backend = CreateBackend(CreateService(handler));

        var request = CreateRequest("Route this message");
        await CollectSingleEventAsync(backend, request);

        var captured = handler.LastRequest
            ?? throw new InvalidOperationException("Expected one HTTP request.");
        Assert.Equal(HttpMethod.Post, captured.Method);
        Assert.Equal("https://api.test.com/v1/chat/completions", captured.RequestUri?.ToString());
        Assert.Equal("Bearer test-key-123", captured.Headers.Authorization?.ToString());

        var body = await ReadBodyAsync(captured);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.Equal("test-model", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());

        var messages = root.GetProperty("messages");
        Assert.Equal(1, messages.GetArrayLength());
        Assert.Equal("user", messages[0].GetProperty("role").GetString());
        Assert.Equal("Route this message", messages[0].GetProperty("content").GetString());

        Assert.False(root.TryGetProperty("tools", out _));
        Assert.False(root.TryGetProperty("tool_choice", out _));
        Assert.False(root.TryGetProperty("permissions", out _));
        Assert.False(root.TryGetProperty("attachments", out _));
        Assert.False(root.TryGetProperty("usage", out _));
        Assert.False(root.TryGetProperty("trace", out _));
        Assert.False(root.TryGetProperty("retry", out _));
        Assert.False(root.TryGetProperty("history", out _));

        Assert.DoesNotContain("test-key-123", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_IssuesExactlyOneHttpRequest_NoRetries()
    {
        var callCount = 0;
        var handler = new CaptureMessageHandler(_ =>
        {
            callCount++;
            return Ok(SuccessBody("once"));
        });
        var backend = CreateBackend(CreateService(handler));

        await CollectSingleEventAsync(backend, "hello");

        Assert.Equal(1, callCount);
    }

    // ── Success paths ───────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_StandardSuccess_YieldsOneMessageCompletedEvent()
    {
        var backend = CreateBackend(CreateService(HttpStatusCode.OK, SuccessBody("Hello from assistant")));

        var events = await CollectEventsAsync(backend, "Hi");

        var terminal = Assert.Single(events);
        Assert.Equal(AgentBackendEventKind.MessageCompleted, terminal.Kind);
        var payload = Assert.IsType<AgentBackendMessageCompletedPayload>(terminal.Payload);
        Assert.Equal("Hello from assistant", payload.AssistantText);
        Assert.NotEqual(default, terminal.OccurredAtUtc);
    }

    [Fact]
    public async Task ExecuteAsync_ClineDataEnvelope_ReturnsAssistantText()
    {
        const string body =
            """
            {"success":true,"data":{"choices":[{"message":{"content":"Cline envelope reply"},"finish_reason":"stop"}]}}
            """;
        var backend = CreateBackend(CreateService(HttpStatusCode.OK, body));

        var events = await CollectEventsAsync(backend, "Hello");

        var terminal = Assert.Single(events);
        var payload = Assert.IsType<AgentBackendMessageCompletedPayload>(terminal.Payload);
        Assert.Equal("Cline envelope reply", payload.AssistantText);
    }

    // ── Failure paths ───────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_NonSuccessHttpStatus_YieldsExecutionFailure()
    {
        var backend = CreateBackend(CreateService(
            HttpStatusCode.Unauthorized,
            "{\"error\": {\"message\": \"Invalid credentials\"}}"));

        var events = await CollectEventsAsync(backend, "Hello");

        var terminal = Assert.Single(events);
        Assert.Equal(AgentBackendEventKind.FailureObserved, terminal.Kind);
        var payload = Assert.IsType<AgentBackendFailurePayload>(terminal.Payload);
        Assert.Equal(AgentFailureKind.Execution, payload.FailureKind);
        Assert.Contains("401", payload.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("Invalid credentials", payload.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("test-key-123", payload.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_HttpRequestException_YieldsTransportFailure()
    {
        var handler = new FaultMessageHandler(new HttpRequestException("Connection refused"));
        var backend = CreateBackend(CreateService(handler));

        var events = await CollectEventsAsync(backend, "Hello");

        var payload = Assert.IsType<AgentBackendFailurePayload>(Assert.Single(events).Payload);
        Assert.Equal(AgentFailureKind.Transport, payload.FailureKind);
        Assert.Contains("Connection refused", payload.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("test-key-123", payload.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MalformedJson_YieldsIndeterminateFailure()
    {
        var backend = CreateBackend(CreateService(HttpStatusCode.OK, "this is not json"));

        var events = await CollectEventsAsync(backend, "Hello");

        var payload = Assert.IsType<AgentBackendFailurePayload>(Assert.Single(events).Payload);
        Assert.Equal(AgentFailureKind.Indeterminate, payload.FailureKind);
        Assert.Contains("Invalid JSON", payload.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_StructurallyInvalidResponse_YieldsIndeterminateFailure()
    {
        var backend = CreateBackend(CreateService(HttpStatusCode.OK, "{\"id\":\"x\",\"model\":\"m\"}"));

        var events = await CollectEventsAsync(backend, "Hello");

        var payload = Assert.IsType<AgentBackendFailurePayload>(Assert.Single(events).Payload);
        Assert.Equal(AgentFailureKind.Indeterminate, payload.FailureKind);
        Assert.Contains("response structure", payload.Reason, StringComparison.OrdinalIgnoreCase);
    }

    // ── Cancellation vs timeout ─────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_CallerCancellation_YieldsCancellationFailure()
    {
        using var cts = new CancellationTokenSource();
        var handler = new DelayMessageHandler(TimeSpan.FromSeconds(30));
        var backend = CreateBackend(CreateService(handler));

        var executeTask = CollectEventsAsync(backend, "Hello", cts.Token);
        cts.Cancel();

        var events = await executeTask;
        var payload = Assert.IsType<AgentBackendFailurePayload>(Assert.Single(events).Payload);
        Assert.Equal(AgentFailureKind.Cancellation, payload.FailureKind);
        Assert.Contains("cancelled", payload.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_HttpClientTimeoutWithoutCallerCancellation_YieldsTimeoutFailure()
    {
        var handler = new DelayMessageHandler(TimeSpan.FromSeconds(30));
        var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(100) };
        var (settings, secrets) = CreateSettingsAndSecrets(DefaultOptions());
        var executionService = new AgentExecutionService(httpClient, settings, secrets);
        var backend = new LegacyOpenAiCompatibleAgentBackend(executionService);

        var events = await CollectEventsAsync(backend, "Hello");

        var payload = Assert.IsType<AgentBackendFailurePayload>(Assert.Single(events).Payload);
        Assert.Equal(AgentFailureKind.Timeout, payload.FailureKind);
        Assert.Contains("cancelled", payload.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_FaultingHandlerTaskCanceledWithoutCallerToken_YieldsTimeoutNotCancellation()
    {
        var handler = new FaultMessageHandler(new TaskCanceledException("The operation was canceled."));
        var backend = CreateBackend(CreateService(handler));

        var events = await CollectEventsAsync(backend, "Hello");

        var payload = Assert.IsType<AgentBackendFailurePayload>(Assert.Single(events).Payload);
        Assert.Equal(AgentFailureKind.Timeout, payload.FailureKind);
    }

    [Fact]
    public async Task ExecuteAsync_ResponseReadCancellation_YieldsCancellationFailure()
    {
        using var cts = new CancellationTokenSource();
        var handler = new SlowReadHandler(TimeSpan.FromSeconds(30));
        var backend = CreateBackend(CreateService(handler));

        var executeTask = CollectEventsAsync(backend, "Hello", cts.Token);
        await Task.Delay(50);
        cts.Cancel();

        var events = await executeTask;
        var payload = Assert.IsType<AgentBackendFailurePayload>(Assert.Single(events).Payload);
        Assert.Equal(AgentFailureKind.Cancellation, payload.FailureKind);
    }

    [Fact]
    public async Task ExecuteAsync_ResponseReadTimeout_YieldsTimeoutFailure()
    {
        var handler = new SlowReadHandler(TimeSpan.Zero, SlowReadMode.ThrowTimeoutOnRead);
        var backend = CreateBackend(CreateService(handler));

        var events = await CollectEventsAsync(backend, "Hello");

        var payload = Assert.IsType<AgentBackendFailurePayload>(Assert.Single(events).Payload);
        Assert.Equal(AgentFailureKind.Timeout, payload.FailureKind);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPostExecutionConfigReadWouldThrow_StillYieldsFailureObserved()
    {
        var (innerSettings, secrets) = CreateSettingsAndSecrets(DefaultOptions());
        var throwingSettings = new ThrowAfterFirstCurrentReadSettingsService(innerSettings);
        var handler = new CaptureMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(
                    "{\"error\":{\"message\":\"Invalid credentials\"}}",
                    Encoding.UTF8,
                    "application/json"),
            });
        var executionService = new AgentExecutionService(
            new HttpClient(handler),
            throwingSettings,
            secrets);
        var backend = new LegacyOpenAiCompatibleAgentBackend(executionService);

        var events = await CollectEventsAsync(backend, "Hello");

        var payload = Assert.IsType<AgentBackendFailurePayload>(Assert.Single(events).Payload);
        Assert.Equal(AgentFailureKind.Execution, payload.FailureKind);
        Assert.Equal(1, throwingSettings.CurrentReadCount);
        Assert.DoesNotContain("test-key-123", payload.Reason, StringComparison.Ordinal);
    }

    // ── Secret boundary ─────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_FailureEvent_DoesNotExposeSentinelSecret()
    {
        var handler = new FaultMessageHandler(
            new HttpRequestException($"Transport failed with {SentinelSecret}"));
        var options = new AgentExecutionOptions
        {
            BaseUrl = "https://api.test.com/v1",
            ApiKey = SentinelSecret,
            Model = "test-model",
        };
        var backend = CreateBackend(CreateService(handler, options));

        var events = await CollectEventsAsync(backend, "Hello");

        var payload = Assert.IsType<AgentBackendFailurePayload>(Assert.Single(events).Payload);
        Assert.DoesNotContain(SentinelSecret, payload.Reason, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", payload.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ConfigResolutionFailure_DoesNotExposeSentinelInBackendEvent()
    {
        var settings = new ThrowingOnCurrentSettingsService(
            $"Configuration unavailable: {SentinelSecret}");
        var secrets = new TestSecretStore();
        var executionService = new AgentExecutionService(
            new HttpClient(new FakeMessageHandler(HttpStatusCode.OK, "{}")),
            settings,
            secrets);
        var backend = new LegacyOpenAiCompatibleAgentBackend(executionService);

        var events = await CollectEventsAsync(backend, "Hello");

        var payload = Assert.IsType<AgentBackendFailurePayload>(Assert.Single(events).Payload);
        Assert.DoesNotContain(SentinelSecret, payload.Reason, StringComparison.Ordinal);
        Assert.Equal("Failed to resolve LLM configuration.", payload.Reason);
    }

    // ── Event ordering ──────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_YieldsExactlyOneTerminalEvent_InDeterministicOrder()
    {
        var backend = CreateBackend(CreateService(HttpStatusCode.OK, SuccessBody("done")));

        var events = await CollectEventsAsync(backend, "hello");

        Assert.Single(events);
        Assert.Equal(AgentBackendEventKind.MessageCompleted, events[0].Kind);
    }

    // ── DI ──────────────────────────────────────────────────────────────────

    [Fact]
    public void AddZaideAgents_RegistersSingleBackend_WithoutNetworkDuringModuleInspection()
    {
        var services = new ServiceCollection();
        services.AddZaideAgents();

        Assert.Equal(10, services.Count);

        var backendDescriptors = services
            .Where(d => d.ServiceType == typeof(IAgentBackend))
            .ToArray();
        Assert.Single(backendDescriptors);
        Assert.Equal(ServiceLifetime.Singleton, backendDescriptors[0].Lifetime);
        Assert.Equal(typeof(LegacyOpenAiCompatibleAgentBackend), backendDescriptors[0].ImplementationType);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static AgentExecutionOptions DefaultOptions() => new()
    {
        BaseUrl = "https://api.test.com/v1",
        ApiKey = "test-key-123",
        Model = "test-model",
    };

    private static string SuccessBody(string content) =>
        JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { message = new { content }, finish_reason = "stop" },
            },
        });

    private static AgentBackendRequest CreateRequest(string messageText) =>
        new(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            ActorId.HumanUser,
            ActorId.PanelSeed("alpha"),
            ConversationEntryId.New(),
            messageText);

    private static AgentBackendExecutionContext CreateContext(string messageText) =>
        new(CreateRequest(messageText), new UnavailableAgentActionBroker());

    private static AgentBackendExecutionContext CreateContext(AgentBackendRequest request) =>
        new(request, new UnavailableAgentActionBroker());

    private LegacyOpenAiCompatibleAgentBackend CreateBackend(AgentExecutionService executionService) =>
        new(executionService);

    private AgentExecutionService CreateService(HttpStatusCode statusCode, string responseBody) =>
        CreateService(new FakeMessageHandler(statusCode, responseBody));

    private AgentExecutionService CreateService(
        HttpMessageHandler handler,
        AgentExecutionOptions? options = null)
    {
        var httpClient = new HttpClient(handler);
        var (settings, secrets) = CreateSettingsAndSecrets(options ?? DefaultOptions());
        return new AgentExecutionService(httpClient, settings, secrets);
    }

    private AgentExecutionService CreateService(
        HttpStatusCode statusCode,
        string responseBody,
        AgentExecutionOptions options) =>
        CreateService(new FakeMessageHandler(statusCode, responseBody), options);

    private (SettingsService settings, TestSecretStore secrets) CreateSettingsAndSecrets(
        AgentExecutionOptions options)
    {
        ClearEnvironment("AGENT_API_URL", "AGENT_MODEL", "AGENT_API_KEY");

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
        {
            secrets.Set("llm.apiKey", options.ApiKey);
        }

        return (settingsService, secrets);
    }

    private static async Task<List<AgentBackendEvent>> CollectEventsAsync(
        LegacyOpenAiCompatibleAgentBackend backend,
        string messageText,
        CancellationToken cancellationToken = default)
    {
        var context = CreateContext(messageText);
        var events = new List<AgentBackendEvent>();
        await foreach (var backendEvent in backend.ExecuteAsync(context, cancellationToken)
                           .ConfigureAwait(false))
        {
            events.Add(backendEvent);
        }

        return events;
    }

    private static async Task CollectSingleEventAsync(
        LegacyOpenAiCompatibleAgentBackend backend,
        string messageText,
        CancellationToken cancellationToken = default)
    {
        var events = await CollectEventsAsync(backend, messageText, cancellationToken);
        Assert.Single(events);
    }

    private static async Task CollectSingleEventAsync(
        LegacyOpenAiCompatibleAgentBackend backend,
        AgentBackendRequest request,
        CancellationToken cancellationToken = default)
    {
        var context = CreateContext(request);
        var events = new List<AgentBackendEvent>();
        await foreach (var backendEvent in backend.ExecuteAsync(context, cancellationToken)
                           .ConfigureAwait(false))
        {
            events.Add(backendEvent);
        }

        Assert.Single(events);
    }

    private static HttpResponseMessage Ok(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private static async Task<string> ReadBodyAsync(HttpRequestMessage request) =>
        request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync().ConfigureAwait(false);

    private static Dictionary<string, string?> CaptureEnvironment(params string[] keys)
    {
        var captured = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            captured[key] = Environment.GetEnvironmentVariable(key);
        }

        return captured;
    }

    private static void RestoreEnvironment(Dictionary<string, string?> captured)
    {
        foreach (var (key, value) in captured)
        {
            if (value is null)
            {
                Environment.SetEnvironmentVariable(key, null);
            }
            else
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    private static void ClearEnvironment(params string[] keys)
    {
        foreach (var key in keys)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    private static void SetEnvironment(string key, string value) =>
        Environment.SetEnvironmentVariable(key, value);

    private sealed class CaptureMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public CaptureMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_responder(request));
        }
    }

    private sealed class DelayMessageHandler : HttpMessageHandler
    {
        private readonly TimeSpan _delay;

        public DelayMessageHandler(TimeSpan delay)
        {
            _delay = delay;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);
            return Ok(SuccessBody("delayed"));
        }
    }

    private sealed class FaultMessageHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public FaultMessageHandler(Exception exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(_exception);
    }

    private sealed class ThrowAfterFirstCurrentReadSettingsService : ISettingsService
    {
        private readonly SettingsService _inner;
        private int _currentReadCount;

        public ThrowAfterFirstCurrentReadSettingsService(SettingsService inner)
        {
            _inner = inner;
        }

        public int CurrentReadCount => _currentReadCount;

        public SettingsModel Current
        {
            get
            {
                _currentReadCount++;
                if (_currentReadCount > 1)
                {
                    throw new InvalidOperationException("Settings unavailable after first read.");
                }

                return _inner.Current;
            }
        }

        public IObservable<SettingsModel> WhenChanged => _inner.WhenChanged;

        public SettingsLoadResult LoadResult => _inner.LoadResult;

        public Task<SettingsMutationResult> UpdateAsync(
            Func<SettingsModel, SettingsModel> producer,
            CancellationToken ct = default) =>
            _inner.UpdateAsync(producer, ct);

        public Task<SettingsMutationResult> ApplyAsync(
            SettingsModel expectedCurrent,
            SettingsModel next,
            CancellationToken ct = default) =>
            _inner.ApplyAsync(expectedCurrent, next, ct);

        public Task<SettingsSaveResult> SaveAsync(CancellationToken ct = default) =>
            _inner.SaveAsync(ct);

        public IObservable<SettingsSaveError> WriteErrors => _inner.WriteErrors;
    }
}
