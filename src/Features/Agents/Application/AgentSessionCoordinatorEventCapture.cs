using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Thread-safe, conversation-scoped session event capture for one admitted
/// coordinator send attempt. Subscribed before admission and disposed after
/// terminal handling.
/// </summary>
internal sealed class AgentSessionCoordinatorEventCapture : IDisposable
{
    private readonly ConversationId _conversationId;
    private readonly ConversationEntryId _messageEntryId;
    private readonly object _sync = new();
    private readonly List<AgentEvent> _events = new();
    private readonly TaskCompletionSource<ExecutionRunId> _admissionTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Action<ExecutionRunId>? _onAdmitted;
    private IDisposable? _subscription;

    public AgentSessionCoordinatorEventCapture(
        ConversationId conversationId,
        ConversationEntryId messageEntryId,
        Action<ExecutionRunId>? onAdmitted = null)
    {
        if (conversationId == default)
        {
            throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        }

        if (messageEntryId == default)
        {
            throw new ArgumentException("Message entry id is required.", nameof(messageEntryId));
        }

        _conversationId = conversationId;
        _messageEntryId = messageEntryId;
        _onAdmitted = onAdmitted;
    }

    public void Subscribe(IObservable<AgentEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        _subscription = events.Subscribe(OnEvent);
    }

    public async Task<ExecutionRunId?> WaitForAdmissionOrRejectionAsync(
        Task<AgentRunSnapshot> sendTask,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sendTask);

        while (true)
        {
            var admittedRunId = TryResolveAdmittedRunId();
            if (admittedRunId is not null)
            {
                return admittedRunId;
            }

            if (sendTask.IsCompleted)
            {
                var snapshot = await sendTask.ConfigureAwait(false);
                if (snapshot.Status == AgentRunStatus.Rejected)
                {
                    return null;
                }

                admittedRunId = TryResolveAdmittedRunId();
                if (admittedRunId is not null)
                {
                    return admittedRunId;
                }

                throw new InvalidOperationException(
                    "Session send completed without admission or structured rejection.");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                admittedRunId = TryResolveAdmittedRunId();
                if (admittedRunId is not null)
                {
                    return admittedRunId;
                }

                cancellationToken.ThrowIfCancellationRequested();
            }

            var completed = await Task.WhenAny(
                    _admissionTcs.Task,
                    sendTask)
                .ConfigureAwait(false);

            if (completed == _admissionTcs.Task)
            {
                return await _admissionTcs.Task.ConfigureAwait(false);
            }

            var completedSnapshot = await sendTask.ConfigureAwait(false);
            if (completedSnapshot.Status == AgentRunStatus.Rejected)
            {
                return null;
            }
        }
    }

    public ExecutionRunId? TryGetAdmittedRunId() => TryResolveAdmittedRunId();

    public IReadOnlyList<AgentEvent> GetEventsForRun(ExecutionRunId runId)
    {
        lock (_sync)
        {
            return _events
                .Where(agentEvent => agentEvent.RunId == runId)
                .ToArray();
        }
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        _subscription = null;
    }

    private ExecutionRunId? TryResolveAdmittedRunId()
    {
        lock (_sync)
        {
            foreach (var agentEvent in _events)
            {
                if (agentEvent.Kind != AgentEventKind.UserMessageAdmitted
                    || agentEvent.Payload is not AgentMessagePayload payload
                    || payload.MessageEntryId != _messageEntryId)
                {
                    continue;
                }

                return agentEvent.RunId;
            }
        }

        return null;
    }

    private void OnEvent(AgentEvent agentEvent)
    {
        if (agentEvent.ConversationId != _conversationId)
        {
            return;
        }

        ExecutionRunId? admittedRunId = null;
        lock (_sync)
        {
            _events.Add(agentEvent);

            if (agentEvent.Kind == AgentEventKind.UserMessageAdmitted
                && agentEvent.Payload is AgentMessagePayload payload
                && payload.MessageEntryId == _messageEntryId)
            {
                admittedRunId = agentEvent.RunId;
            }
        }

        if (admittedRunId is not null)
        {
            _onAdmitted?.Invoke(admittedRunId.Value);
            _admissionTcs.TrySetResult(admittedRunId.Value);
        }
    }
}
