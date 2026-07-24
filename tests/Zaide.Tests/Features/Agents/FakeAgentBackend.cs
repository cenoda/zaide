using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;

namespace Zaide.Tests.Features.Agents;

/// <summary>
/// Configurable in-memory agent backend for coordinator and session tests.
/// </summary>
internal sealed class FakeAgentBackend : IAgentBackend
{
    private readonly Queue<FakeBackendPlan> _plans = new();

    public FakeAgentBackend(AgentBackendId backendId)
    {
        BackendId = backendId;
        BackendVersion = "test-1.0.0";
        CapabilitySnapshot = AgentCapabilitySnapshot.CreateInitial(
            backendId,
            new[]
            {
                AgentCapabilityRow.Create(
                    AgentCapabilityId.MessageCompletion,
                    AgentCapabilityState.Create(
                        advertised: AgentCapabilityFactValue.Supported,
                        available: AgentCapabilityFactValue.Supported,
                        configured: AgentCapabilityFactValue.Supported,
                        permitted: AgentCapabilityFactValue.Unknown,
                        degraded: AgentCapabilityFactValue.NotSupported,
                        currentlyUsable: AgentCapabilityFactValue.Supported)),
            });
    }

    public int ExecuteCallCount { get; private set; }

    public AgentBackendId BackendId { get; }

    public string BackendVersion { get; }

    public AgentCapabilitySnapshot CapabilitySnapshot { get; }

    public void SetCompletion(params string[] assistantTexts)
    {
        _plans.Clear();
        foreach (var text in assistantTexts)
        {
            _plans.Enqueue(FakeBackendPlan.Completion(text));
        }
    }

    public void SetDelayedCompletion(TimeSpan delay, string assistantText)
    {
        _plans.Clear();
        _plans.Enqueue(FakeBackendPlan.DelayedCompletion(delay, assistantText));
    }

    public void SetGatedCompletion(TaskCompletionSource<string> gate, string assistantText)
    {
        _plans.Clear();
        _plans.Enqueue(FakeBackendPlan.GatedCompletion(gate, assistantText));
    }

    public void SetFailure(AgentFailureKind failureKind, string reason)
    {
        _plans.Clear();
        _plans.Enqueue(FakeBackendPlan.Failure(failureKind, reason));
    }

    public void SetEnumerationFault(string message)
    {
        _plans.Clear();
        _plans.Enqueue(FakeBackendPlan.EnumerationFault(message));
    }

    public void SetLateCompletionIgnoringCancellation(TimeSpan delay, string assistantText)
    {
        _plans.Clear();
        _plans.Enqueue(FakeBackendPlan.LateCompletionIgnoringCancellation(delay, assistantText));
    }

    public async IAsyncEnumerable<AgentBackendEvent> ExecuteAsync(
        AgentBackendExecutionContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ExecuteCallCount++;
        if (_plans.Count == 0)
        {
            throw new InvalidOperationException("No fake backend plan configured.");
        }

        var plan = _plans.Dequeue();
        if (plan.EnumerationFaultMessage is { } faultMessage)
        {
            throw new InvalidOperationException(faultMessage);
        }

        if (plan.Gate is { } gate)
        {
            var assistantText = await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            yield return new AgentBackendEvent(
                AgentBackendEventKind.MessageCompleted,
                DateTimeOffset.UtcNow,
                new AgentBackendMessageCompletedPayload(assistantText));
            yield break;
        }

        if (plan.Delay is { } delay)
        {
            if (plan.IgnoreCancellation)
            {
                await Task.Delay(delay).ConfigureAwait(false);
            }
            else
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        if (!plan.IgnoreCancellation)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (plan.FailureKind is { } failureKind)
        {
            yield return new AgentBackendEvent(
                AgentBackendEventKind.FailureObserved,
                DateTimeOffset.UtcNow,
                new AgentBackendFailurePayload(failureKind, plan.FailureReason!));
            yield break;
        }

        yield return new AgentBackendEvent(
            AgentBackendEventKind.MessageCompleted,
            DateTimeOffset.UtcNow,
            new AgentBackendMessageCompletedPayload(plan.AssistantText!));
    }

    private sealed record FakeBackendPlan(
        TimeSpan? Delay,
        string? AssistantText,
        AgentFailureKind? FailureKind,
        string? FailureReason,
        string? EnumerationFaultMessage,
        bool IgnoreCancellation,
        TaskCompletionSource<string>? Gate)
    {
        public static FakeBackendPlan Completion(string text) =>
            new(null, text, null, null, null, false, null);

        public static FakeBackendPlan DelayedCompletion(TimeSpan delay, string text) =>
            new(delay, text, null, null, null, false, null);

        public static FakeBackendPlan GatedCompletion(TaskCompletionSource<string> gate, string text) =>
            new(null, text, null, null, null, false, gate);

        public static FakeBackendPlan Failure(AgentFailureKind kind, string reason) =>
            new(null, null, kind, reason, null, false, null);

        public static FakeBackendPlan EnumerationFault(string message) =>
            new(null, null, null, null, message, false, null);

        public static FakeBackendPlan LateCompletionIgnoringCancellation(TimeSpan delay, string text) =>
            new(delay, text, null, null, null, true, null);
    }
}
