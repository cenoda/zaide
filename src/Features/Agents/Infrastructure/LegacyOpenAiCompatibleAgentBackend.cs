using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Infrastructure;

/// <summary>
/// Phase 15 M3a compatibility backend that wraps the live
/// <see cref="IAgentExecutionService"/> HTTP path without duplicating transport.
/// Emits one terminal backend observation per admitted run attempt.
/// </summary>
internal sealed class LegacyOpenAiCompatibleAgentBackend : IAgentBackend
{
    internal const string BackendIdValue = "backend:legacy-openai-compatible";

    internal const string BackendVersionValue = "legacy-openai-compatible/1";

    private static readonly AgentBackendId StableBackendId =
        AgentBackendId.FromValue(BackendIdValue);

    private readonly AgentExecutionService _executionService;
    private readonly object _capabilitySync = new();
    private AgentCapabilitySnapshot _capabilitySnapshot;
    private bool _capabilityInitialized;
    private CapabilityObservationState _capabilityObservationState;

    private enum CapabilityObservationState
    {
        Unconfigured,
        Configured,
        ResolutionUnavailable,
    }

    public LegacyOpenAiCompatibleAgentBackend(IAgentExecutionService executionService)
    {
        if (executionService is not AgentExecutionService concrete)
        {
            throw new ArgumentException(
                "Legacy OpenAI-compatible backend requires the concrete AgentExecutionService instance.",
                nameof(executionService));
        }

        _executionService = concrete;
        _capabilitySnapshot = AgentCapabilitySnapshot.CreateInitial(
            StableBackendId,
            CreateCapabilityRows(configured: false),
            version: 1);
    }

    public AgentBackendId BackendId => StableBackendId;

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
        AgentBackendRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        AgentExecutionResult? result = null;
        AgentBackendEvent? faultEvent = null;
        try
        {
            result = await _executionService
                .ExecuteAsync(request.MessageText, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            faultEvent = CreateFailureEvent(
                AgentFailureKind.Indeterminate,
                $"Execution failed: {ex.GetType().Name}");
        }

        if (faultEvent is not null)
        {
            yield return faultEvent;
            yield break;
        }

        var executionResult = result!;

        var occurredAtUtc = DateTimeOffset.UtcNow;

        if (executionResult.IsSuccess)
        {
            yield return new AgentBackendEvent(
                AgentBackendEventKind.MessageCompleted,
                occurredAtUtc,
                new AgentBackendMessageCompletedPayload(executionResult.ResponseText!));
            yield break;
        }

        var failureKind = executionResult.FailureKind ?? AgentFailureKind.Indeterminate;
        var reason = executionResult.ErrorMessage ?? "Execution failed.";

        yield return new AgentBackendEvent(
            AgentBackendEventKind.FailureObserved,
            occurredAtUtc,
            new AgentBackendFailurePayload(failureKind, reason));
    }

    private void RefreshCapabilitySnapshotLocked()
    {
        try
        {
            var options = _executionService.BuildEffectiveOptions();
            var observation = IsConfigured(options)
                ? CapabilityObservationState.Configured
                : CapabilityObservationState.Unconfigured;
            ApplyObservationStateLocked(observation);
        }
        catch
        {
            ApplyObservationStateLocked(CapabilityObservationState.ResolutionUnavailable);
        }
    }

    private void ApplyObservationStateLocked(CapabilityObservationState observation)
    {
        if (!_capabilityInitialized)
        {
            _capabilityObservationState = observation;
            _capabilitySnapshot = CreateSnapshotForObservation(observation, version: 1);
            _capabilityInitialized = true;
            return;
        }

        if (_capabilityObservationState == observation)
        {
            return;
        }

        _capabilityObservationState = observation;
        _capabilitySnapshot = CreateSnapshotForObservation(
            observation,
            version: _capabilitySnapshot.Version + 1);
    }

    private static AgentCapabilitySnapshot CreateSnapshotForObservation(
        CapabilityObservationState observation,
        int version) =>
        AgentCapabilitySnapshot.CreateInitial(
            StableBackendId,
            observation switch
            {
                CapabilityObservationState.Configured => CreateCapabilityRows(configured: true),
                CapabilityObservationState.Unconfigured => CreateCapabilityRows(configured: false),
                CapabilityObservationState.ResolutionUnavailable => CreateResolutionUnavailableCapabilityRows(),
                _ => throw new ArgumentOutOfRangeException(nameof(observation), observation, null),
            },
            version);

    private static bool IsConfigured(AgentExecutionOptions options) =>
        !string.IsNullOrWhiteSpace(options.ApiKey)
        && !string.IsNullOrWhiteSpace(options.BaseUrl)
        && !string.IsNullOrWhiteSpace(options.Model);

    private static IEnumerable<AgentCapabilityRow> CreateCapabilityRows(bool configured)
    {
        yield return AgentCapabilityRow.Create(
            AgentCapabilityId.MessageCompletion,
            configured
                ? AgentCapabilityState.Create(
                    advertised: AgentCapabilityFactValue.Supported,
                    available: AgentCapabilityFactValue.Supported,
                    configured: AgentCapabilityFactValue.Supported,
                    permitted: AgentCapabilityFactValue.Unknown,
                    degraded: AgentCapabilityFactValue.NotSupported,
                    currentlyUsable: AgentCapabilityFactValue.Supported)
                : AgentCapabilityState.Create(
                    advertised: AgentCapabilityFactValue.Supported,
                    available: AgentCapabilityFactValue.Unavailable,
                    configured: AgentCapabilityFactValue.Unavailable,
                    permitted: AgentCapabilityFactValue.Unknown,
                    degraded: AgentCapabilityFactValue.NotSupported,
                    currentlyUsable: AgentCapabilityFactValue.Unavailable));

        yield return AgentCapabilityRow.Create(
            AgentCapabilityId.Streaming,
            AgentCapabilityState.Create(
                advertised: AgentCapabilityFactValue.Supported,
                available: AgentCapabilityFactValue.NotSupported,
                configured: AgentCapabilityFactValue.Unknown,
                permitted: AgentCapabilityFactValue.Unknown,
                degraded: AgentCapabilityFactValue.NotSupported,
                currentlyUsable: AgentCapabilityFactValue.NotSupported));

        yield return AgentCapabilityRow.Create(
            AgentCapabilityId.Cancellation,
            configured
                ? AgentCapabilityState.Create(
                    advertised: AgentCapabilityFactValue.Supported,
                    available: AgentCapabilityFactValue.Supported,
                    configured: AgentCapabilityFactValue.Supported,
                    permitted: AgentCapabilityFactValue.Unknown,
                    degraded: AgentCapabilityFactValue.NotSupported,
                    currentlyUsable: AgentCapabilityFactValue.Supported)
                : AgentCapabilityState.Create(
                    advertised: AgentCapabilityFactValue.Supported,
                    available: AgentCapabilityFactValue.Unavailable,
                    configured: AgentCapabilityFactValue.Unavailable,
                    permitted: AgentCapabilityFactValue.Unknown,
                    degraded: AgentCapabilityFactValue.NotSupported,
                    currentlyUsable: AgentCapabilityFactValue.Unavailable));

        yield return CreateUnavailableRow(AgentCapabilityId.Attachments);
        yield return CreateUnavailableRow(AgentCapabilityId.Tools);
        yield return CreateUnavailableRow(AgentCapabilityId.Permissions);
        yield return CreateUnavailableRow(AgentCapabilityId.Resume);
        yield return CreateUnavailableRow(AgentCapabilityId.Reconnect);
        yield return CreateUnavailableRow(AgentCapabilityId.UsageReporting);
        yield return CreateUnavailableRow(AgentCapabilityId.RawTrace);
    }

    private static IEnumerable<AgentCapabilityRow> CreateResolutionUnavailableCapabilityRows()
    {
        yield return AgentCapabilityRow.Create(
            AgentCapabilityId.MessageCompletion,
            AgentCapabilityState.Create(
                advertised: AgentCapabilityFactValue.Supported,
                available: AgentCapabilityFactValue.Unavailable,
                configured: AgentCapabilityFactValue.Unknown,
                permitted: AgentCapabilityFactValue.Unknown,
                degraded: AgentCapabilityFactValue.NotSupported,
                currentlyUsable: AgentCapabilityFactValue.Unavailable));

        yield return AgentCapabilityRow.Create(
            AgentCapabilityId.Streaming,
            AgentCapabilityState.Create(
                advertised: AgentCapabilityFactValue.Supported,
                available: AgentCapabilityFactValue.NotSupported,
                configured: AgentCapabilityFactValue.Unknown,
                permitted: AgentCapabilityFactValue.Unknown,
                degraded: AgentCapabilityFactValue.NotSupported,
                currentlyUsable: AgentCapabilityFactValue.NotSupported));

        yield return AgentCapabilityRow.Create(
            AgentCapabilityId.Cancellation,
            AgentCapabilityState.Create(
                advertised: AgentCapabilityFactValue.Supported,
                available: AgentCapabilityFactValue.Unavailable,
                configured: AgentCapabilityFactValue.Unknown,
                permitted: AgentCapabilityFactValue.Unknown,
                degraded: AgentCapabilityFactValue.NotSupported,
                currentlyUsable: AgentCapabilityFactValue.Unavailable));

        yield return CreateUnavailableRow(AgentCapabilityId.Attachments);
        yield return CreateUnavailableRow(AgentCapabilityId.Tools);
        yield return CreateUnavailableRow(AgentCapabilityId.Permissions);
        yield return CreateUnavailableRow(AgentCapabilityId.Resume);
        yield return CreateUnavailableRow(AgentCapabilityId.Reconnect);
        yield return CreateUnavailableRow(AgentCapabilityId.UsageReporting);
        yield return CreateUnavailableRow(AgentCapabilityId.RawTrace);
    }

    private static AgentCapabilityRow CreateUnavailableRow(AgentCapabilityId capabilityId) =>
        AgentCapabilityRow.Create(
            capabilityId,
            AgentCapabilityState.Create(
                advertised: AgentCapabilityFactValue.Unavailable,
                available: AgentCapabilityFactValue.Unavailable,
                configured: AgentCapabilityFactValue.Unavailable,
                permitted: AgentCapabilityFactValue.Unknown,
                degraded: AgentCapabilityFactValue.NotSupported,
                currentlyUsable: AgentCapabilityFactValue.Unavailable));

    private static AgentBackendEvent CreateFailureEvent(AgentFailureKind failureKind, string reason) =>
        new(
            AgentBackendEventKind.FailureObserved,
            DateTimeOffset.UtcNow,
            new AgentBackendFailurePayload(failureKind, reason));
}
