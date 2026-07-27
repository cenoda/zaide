using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Infrastructure;

/// <summary>
/// Phase 19 Native Harness backend implementing the tool-calling execution loop.
/// </summary>
internal sealed class NativeHarnessAgentBackend : IAgentActionRequestCapableBackend
{
    internal const string BackendVersionValue = "zaide-native-harness/1";

    private readonly NativeHarnessLoopRunner _loopRunner;
    private readonly INativeHarnessProviderOptionsSource _optionsSource;
    private readonly object _capabilitySync = new();
    private AgentCapabilitySnapshot _capabilitySnapshot;

    public NativeHarnessAgentBackend(
        AgentExecutionService executionService,
        INativeHarnessProviderTransport transport,
        INativeHarnessPriorConversationReader priorConversationReader)
        : this(
            new NativeHarnessProviderOptionsSource(executionService),
            transport,
            priorConversationReader)
    {
    }

    public NativeHarnessAgentBackend(
        INativeHarnessProviderOptionsSource optionsSource,
        INativeHarnessProviderTransport transport,
        INativeHarnessPriorConversationReader priorConversationReader)
    {
        _optionsSource = optionsSource
            ?? throw new ArgumentNullException(nameof(optionsSource));
        _loopRunner = new NativeHarnessLoopRunner(
            optionsSource,
            transport ?? throw new ArgumentNullException(nameof(transport)),
            priorConversationReader
                ?? throw new ArgumentNullException(nameof(priorConversationReader)));

        _capabilitySnapshot = NativeHarnessCapabilityRows.CreateInitialSnapshot(
            providerConfigured: false,
            workspaceCaptured: false,
            contextManifestPresent: false,
            streamingSupportedByProvider: true);
    }

    internal NativeHarnessAgentBackend(NativeHarnessLoopRunner loopRunner)
    {
        _loopRunner = loopRunner ?? throw new ArgumentNullException(nameof(loopRunner));
        _optionsSource = new NullNativeHarnessProviderOptionsSource();
        _capabilitySnapshot = NativeHarnessCapabilityRows.CreateInitialSnapshot(
            providerConfigured: false,
            workspaceCaptured: false,
            contextManifestPresent: false,
            streamingSupportedByProvider: true);
    }

    public AgentBackendId BackendId => AgentBackendIds.NativeHarness;

    public string BackendVersion => BackendVersionValue;

    public AgentCapabilitySnapshot CapabilitySnapshot
    {
        get
        {
            lock (_capabilitySync)
            {
                RefreshCapabilitySnapshotLocked();
                return _capabilitySnapshot;
            }
        }
    }

    public async IAsyncEnumerable<AgentBackendEvent> ExecuteAsync(
        AgentBackendExecutionContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        NativeHarnessRunOutcome? outcome = null;
        AgentBackendEvent? faultEvent = null;
        try
        {
            outcome = await _loopRunner.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            faultEvent = CreateFailureEvent(
                AgentFailureKind.Cancellation,
                "Run was cancelled.");
        }
        catch (Exception ex)
        {
            faultEvent = CreateFailureEvent(
                AgentFailureKind.Indeterminate,
                $"Harness execution failed: {ex.GetType().Name}");
        }

        if (faultEvent is not null)
        {
            yield return faultEvent;
            yield break;
        }

        foreach (var backendEvent in MapOutcome(outcome!))
        {
            yield return backendEvent;
        }
    }

    private void RefreshCapabilitySnapshotLocked()
    {
        try
        {
            var options = _optionsSource.ResolveOptions();
            var configured = options is not null && IsConfigured(options);
            _capabilitySnapshot = NativeHarnessCapabilityRows.CreateInitialSnapshot(
                providerConfigured: configured,
                workspaceCaptured: false,
                contextManifestPresent: false,
                streamingSupportedByProvider: true);
        }
        catch
        {
            _capabilitySnapshot = NativeHarnessCapabilityRows.CreateInitialSnapshot(
                providerConfigured: false,
                workspaceCaptured: false,
                contextManifestPresent: false,
                streamingSupportedByProvider: true);
        }
    }

    private static bool IsConfigured(AgentExecutionOptions options) =>
        !string.IsNullOrWhiteSpace(options.ApiKey)
        && !string.IsNullOrWhiteSpace(options.BaseUrl)
        && !string.IsNullOrWhiteSpace(options.Model);

    private static IEnumerable<AgentBackendEvent> MapOutcome(NativeHarnessRunOutcome outcome)
    {
        var occurredAtUtc = DateTimeOffset.UtcNow;
        switch (outcome.TerminationKind)
        {
            case NativeHarnessRunTerminationKind.Completed:
                yield return new AgentBackendEvent(
                    AgentBackendEventKind.MessageCompleted,
                    occurredAtUtc,
                    new AgentBackendMessageCompletedPayload(outcome.FinalAssistantText!));
                yield break;
            case NativeHarnessRunTerminationKind.Cancelled:
                yield return new AgentBackendEvent(
                    AgentBackendEventKind.FailureObserved,
                    occurredAtUtc,
                    new AgentBackendFailurePayload(
                        AgentFailureKind.Cancellation,
                        outcome.FailureReason ?? "Run was cancelled."));
                yield break;
            case NativeHarnessRunTerminationKind.TurnBudgetExceeded:
                yield return new AgentBackendEvent(
                    AgentBackendEventKind.FailureObserved,
                    occurredAtUtc,
                    new AgentBackendFailurePayload(
                        AgentFailureKind.Execution,
                        outcome.FailureReason ?? "Model turn budget exceeded."));
                yield break;
            case NativeHarnessRunTerminationKind.Indeterminate:
                yield return new AgentBackendEvent(
                    AgentBackendEventKind.FailureObserved,
                    occurredAtUtc,
                    new AgentBackendFailurePayload(
                        AgentFailureKind.Indeterminate,
                        outcome.FailureReason ?? "Run outcome was indeterminate."));
                yield break;
            case NativeHarnessRunTerminationKind.Failed:
            default:
                yield return new AgentBackendEvent(
                    AgentBackendEventKind.FailureObserved,
                    occurredAtUtc,
                    new AgentBackendFailurePayload(
                        AgentFailureKind.Execution,
                        outcome.FailureReason ?? "Harness execution failed."));
                yield break;
        }
    }

    private static AgentBackendEvent CreateFailureEvent(AgentFailureKind failureKind, string reason) =>
        new(
            AgentBackendEventKind.FailureObserved,
            DateTimeOffset.UtcNow,
            new AgentBackendFailurePayload(failureKind, reason));

    private sealed class NullNativeHarnessProviderOptionsSource : INativeHarnessProviderOptionsSource
    {
        public AgentExecutionOptions? ResolveOptions() => null;
    }
}
