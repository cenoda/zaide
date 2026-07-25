using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;

namespace Zaide.Tests.Features.Agents;

/// <summary>
/// Repository-owned fake backend that invokes the run-scoped action broker for
/// Phase 17 M8 session/event integration tests. Not registered in production DI.
/// </summary>
internal sealed class FakeActionRequesterBackend : IAgentActionRequestCapableBackend
{
    internal const string BackendIdValue = "backend:fake-action-requester";

    private readonly Queue<FakeActionRequestPlan> _plans = new();

    public FakeActionRequesterBackend()
    {
        BackendId = AgentBackendId.FromValue(BackendIdValue);
        BackendVersion = "fake-action-requester/1";
        CapabilitySnapshot = AgentCapabilitySnapshot.CreateInitial(
            BackendId,
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
                AgentCapabilityRow.Create(
                    AgentCapabilityId.Tools,
                    AgentCapabilityState.Create(
                        advertised: AgentCapabilityFactValue.Unavailable,
                        available: AgentCapabilityFactValue.Unavailable,
                        configured: AgentCapabilityFactValue.Unavailable,
                        permitted: AgentCapabilityFactValue.Unknown,
                        degraded: AgentCapabilityFactValue.NotSupported,
                        currentlyUsable: AgentCapabilityFactValue.Unavailable)),
                AgentCapabilityRow.Create(
                    AgentCapabilityId.Permissions,
                    AgentCapabilityState.Create(
                        advertised: AgentCapabilityFactValue.Unavailable,
                        available: AgentCapabilityFactValue.Unavailable,
                        configured: AgentCapabilityFactValue.Unavailable,
                        permitted: AgentCapabilityFactValue.Unknown,
                        degraded: AgentCapabilityFactValue.NotSupported,
                        currentlyUsable: AgentCapabilityFactValue.Unavailable)),
            });
    }

    public AgentBackendId BackendId { get; }

    public string BackendVersion { get; }

    public AgentCapabilitySnapshot CapabilitySnapshot { get; }

    public int ExecuteCallCount { get; private set; }

    public void SetReadAndComplete(string relativePath, string? correlationKey = null)
    {
        _plans.Clear();
        _plans.Enqueue(FakeActionRequestPlan.ReadThenComplete(relativePath, correlationKey));
    }

    public void SetDelayedAction(
        TimeSpan delay,
        Func<IAgentActionBroker, CancellationToken, ValueTask<AgentActionResult>> action,
        string assistantText = "done")
    {
        _plans.Clear();
        _plans.Enqueue(new FakeActionRequestPlan(delay, action, assistantText, null, null));
    }

    public async IAsyncEnumerable<AgentBackendEvent> ExecuteAsync(
        AgentBackendExecutionContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ExecuteCallCount++;

        if (_plans.Count == 0)
        {
            throw new InvalidOperationException("No fake action requester plan configured.");
        }

        var plan = _plans.Dequeue();
        if (plan.Delay is { } delay)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (plan.Action is { } action)
        {
            _ = await action(context.Actions, cancellationToken).ConfigureAwait(false);
        }
        else if (plan.ReadPath is { } readPath)
        {
            _ = await context.Actions.RequestAsync(
                new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize(readPath)),
                plan.CorrelationKey,
                cancellationToken).ConfigureAwait(false);
        }

        yield return new AgentBackendEvent(
            AgentBackendEventKind.MessageCompleted,
            DateTimeOffset.UtcNow,
            new AgentBackendMessageCompletedPayload(plan.AssistantText ?? "done"));
    }

    private sealed record FakeActionRequestPlan(
        TimeSpan? Delay,
        Func<IAgentActionBroker, CancellationToken, ValueTask<AgentActionResult>>? Action,
        string? AssistantText,
        string? ReadPath,
        string? CorrelationKey)
    {
        public static FakeActionRequestPlan ReadThenComplete(string readPath, string? correlationKey) =>
            new(null, null, "done", readPath, correlationKey);
    }
}

