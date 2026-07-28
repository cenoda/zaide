using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Infrastructure;
using Zaide.Tests.Features.Settings.Infrastructure;

namespace Zaide.Tests.Features.Agents;

/// <summary>
/// Deterministic provider transport for Phase 19 harness tests.
/// </summary>
internal sealed class ScriptedNativeHarnessProviderTransport : INativeHarnessProviderTransport
{
    private readonly Queue<NativeHarnessProviderResponse> _responses = new();

    public NativeHarnessProviderRequest? LastRequest { get; private set; }

    public IReadOnlyList<NativeHarnessProviderRequest> Requests { get; private set; } =
        Array.Empty<NativeHarnessProviderRequest>();

    private readonly List<NativeHarnessProviderRequest> _requests = new();

    public void Enqueue(NativeHarnessProviderResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        _responses.Enqueue(response);
    }

    public Task<NativeHarnessProviderResponse> CompleteChatAsync(
        AgentExecutionOptions options,
        NativeHarnessProviderRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        LastRequest = request;
        _requests.Add(request);
        Requests = _requests.ToArray();

        if (_responses.Count == 0)
        {
            return Task.FromResult(NativeHarnessProviderResponse.Failure(
                "No scripted provider response remaining.",
                AgentFailureKind.Indeterminate));
        }

        return Task.FromResult(_responses.Dequeue());
    }
}

/// <summary>
/// Records broker dispatch for Phase 19 harness tests.
/// </summary>
internal sealed class RecordingAgentActionBroker : IAgentActionBroker
{
    private readonly Dictionary<AgentActionKind, AgentActionResult> _results = new();
    private readonly List<AgentActionPayload> _payloads = new();

    public IReadOnlyList<AgentActionPayload> Payloads => _payloads;

    public void SetResult(AgentActionKind kind, AgentActionResult result) =>
        _results[kind] = result;

    public void Revoke()
    {
        Revoked = true;
    }

    public bool Revoked { get; private set; }

    public ValueTask<AgentActionResult> RequestAsync(
        AgentActionPayload payload,
        string? correlationKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        _payloads.Add(payload);

        if (Revoked)
        {
            return ValueTask.FromResult(new AgentActionResult(
                AgentActionId.New(),
                AgentActionAttemptId.New(),
                AgentActionResultKind.Denied,
                AgentActionFailureKind.BrokerRevoked,
                "Broker revoked."));
        }

        if (_results.TryGetValue(payload.Kind, out var result))
        {
            return ValueTask.FromResult(result);
        }

        return ValueTask.FromResult(new AgentActionResult(
            AgentActionId.New(),
            AgentActionAttemptId.New(),
            AgentActionResultKind.Succeeded,
            failureKind: null,
            summary: $"{payload.Kind} succeeded."));
    }
}

internal static class Phase19HarnessTestFactory
{
    public static AgentExecutionService CreateExecutionService(
        string tempDir,
        AgentExecutionOptions? options = null,
        IList<IDisposable>? disposableTracker = null)
    {
        options ??= new AgentExecutionOptions
        {
            BaseUrl = "https://api.test.com/v1",
            ApiKey = "test-key",
            Model = "test-model",
        };

        var settingsPath = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + "_settings.json");
        var lkgPath = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + "_lkg.json");
        var tmpPath = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + "_tmp.json");
        var llm = new LlmSettings(options.BaseUrl, options.Model, "secret-store");
        File.WriteAllText(settingsPath, SettingsSerializer.Serialize(SettingsModel.Defaults with { Llm = llm }));

        var settings = new SettingsService(
            settingsPath,
            lkgPath,
            tmpPath,
            new SettingsMigrator(Array.Empty<ISettingsMigration>()));
        var secrets = new TestSecretStore();
        secrets.Set("llm.apiKey", options.ApiKey);
        var service = new AgentExecutionService(new HttpClient(new HttpClientHandler()), settings, secrets);

        if (disposableTracker is not null)
        {
            disposableTracker.Add(settings);
        }

        return service;
    }
}
