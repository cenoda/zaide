using System;
using System.Collections.Generic;
using System.Linq;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Authoritative normalized session event to conversation entry projection.
/// Subscribes to <see cref="IAgentSessionService.Events"/> and writes typed entries
/// to <see cref="IConversationStore"/>.
/// </summary>
internal sealed class AgentConversationEventProjection : IDisposable
{
    private readonly IConversationStore _conversationStore;
    private readonly IActorCatalog? _actorCatalog;
    private readonly IDisposable? _subscription;
    private readonly object _sync = new();

    private readonly HashSet<ConversationEntryId> _projectedMessageEntryIds = new();
    private readonly HashSet<ExecutionRunId> _admittedRunIds = new();
    private readonly HashSet<ExecutionRunId> _projectedTerminalRunIds = new();
    private readonly HashSet<AgentActionId> _projectedActionSummaryIds = new();

    public AgentConversationEventProjection(
        AgentEventStream stream,
        IConversationStore conversationStore,
        IActorCatalog? actorCatalog = null)
        : this(stream?.Events!, conversationStore, actorCatalog)
    {
    }

    public AgentConversationEventProjection(
        IObservable<AgentEvent> events,
        IConversationStore conversationStore,
        IActorCatalog? actorCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(events);
        _conversationStore = conversationStore ?? throw new ArgumentNullException(nameof(conversationStore));
        _actorCatalog = actorCatalog;

        _subscription = events.Subscribe(OnEvent);
    }

    public static ConversationEntry ProjectRoutingFailure(
        IConversationStore conversationStore,
        ConversationId conversationId,
        ActorId author,
        ExecutionRunId runId,
        string failureReason)
    {
        ArgumentNullException.ThrowIfNull(conversationStore);
        if (conversationId == default)
        {
            throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        }

        if (author == default)
        {
            throw new ArgumentException("Author is required.", nameof(author));
        }

        if (runId == default)
        {
            throw new ArgumentException("Run id is required.", nameof(runId));
        }

        if (string.IsNullOrWhiteSpace(failureReason))
        {
            throw new ArgumentException("Failure reason is required.", nameof(failureReason));
        }

        var runCorrelation = ExecutionRunCorrelation.ToEntryCorrelation(runId);
        if (conversationStore.TryGet(conversationId, out var conversation)
            && conversation.Entries.Any(e => e.CorrelationId == runCorrelation && e.Kind == ConversationEntryKind.RoutingFailure))
        {
            return conversation.Entries.First(e => e.CorrelationId == runCorrelation && e.Kind == ConversationEntryKind.RoutingFailure);
        }

        var entry = ConversationEntry.RoutingFailure(
            ConversationEntryId.New(),
            author,
            DateTimeOffset.UtcNow,
            failureReason,
            runCorrelation);

        conversationStore.AppendEntry(conversationId, entry);
        return entry;
    }

    public void Dispose()
    {
        _subscription?.Dispose();
    }

    private void OnEvent(AgentEvent agentEvent)
    {
        if (agentEvent is null)
        {
            return;
        }

        lock (_sync)
        {
            switch (agentEvent.Kind)
            {
                case AgentEventKind.UserMessageAdmitted:
                    ProjectUserMessageAdmitted(agentEvent);
                    break;

                case AgentEventKind.AssistantMessageCompleted:
                    ProjectAssistantMessageCompleted(agentEvent);
                    break;

                case AgentEventKind.FailureReported:
                    ProjectFailureReported(agentEvent);
                    break;

                case AgentEventKind.RunCancelled:
                case AgentEventKind.RunTimedOut:
                case AgentEventKind.RunDisconnected:
                case AgentEventKind.RunIndeterminate:
                case AgentEventKind.RunFailed:
                    ProjectRunTerminalFailure(agentEvent);
                    break;

                case AgentEventKind.RunRejected:
                    // Rejections are not admitted runs and produce no conversation entry.
                    break;

                case AgentEventKind.ActionResultReported:
                    ProjectActionResultReported(agentEvent);
                    break;
            }
        }
    }

    private void ProjectActionResultReported(AgentEvent agentEvent)
    {
        if (agentEvent.Payload is not AgentActionFactPayload payload)
        {
            return;
        }

        if (payload.ResultKind is null)
        {
            return;
        }

        if (_projectedActionSummaryIds.Contains(payload.ActionId))
        {
            return;
        }

        if (!_conversationStore.TryGet(agentEvent.ConversationId, out var conversation))
        {
            return;
        }

        var runCorrelation = ExecutionRunCorrelation.ToEntryCorrelation(agentEvent.RunId);
        var authorActorId = ResolveAgentAuthor(conversation);
        var headline = payload.ActionKind switch
        {
            AgentActionKind.ReadFile => "Read file",
            AgentActionKind.CreateFile => "Create file",
            AgentActionKind.ReplaceFile => "Replace file",
            AgentActionKind.DeleteFile => "Delete file",
            AgentActionKind.ExecuteCommand => "Run command",
            _ => "Agent action",
        };
        var content = $"{headline}: {payload.Summary.Text} ({payload.ResultKind})";
        var entry = ConversationEntry.SystemNotification(
            ConversationEntryId.New(),
            authorActorId,
            agentEvent.OccurredAtUtc,
            content,
            runCorrelation);

        _conversationStore.AppendEntry(agentEvent.ConversationId, entry);
        _projectedActionSummaryIds.Add(payload.ActionId);
    }

    private void ProjectUserMessageAdmitted(AgentEvent agentEvent)
    {
        if (agentEvent.Payload is not AgentMessagePayload payload)
        {
            return;
        }

        _admittedRunIds.Add(agentEvent.RunId);

        if (_projectedMessageEntryIds.Contains(payload.MessageEntryId))
        {
            return;
        }

        if (!_conversationStore.TryGet(agentEvent.ConversationId, out var conversation))
        {
            return;
        }

        var runCorrelation = ExecutionRunCorrelation.ToEntryCorrelation(agentEvent.RunId);
        if (conversation.Entries.Any(e => e.Id == payload.MessageEntryId
                                         || (e.CorrelationId == runCorrelation && e.Kind == ConversationEntryKind.UserChat)))
        {
            _projectedMessageEntryIds.Add(payload.MessageEntryId);
            return;
        }

        var entry = ConversationEntry.UserChat(
            payload.MessageEntryId,
            ActorId.HumanUser,
            agentEvent.OccurredAtUtc,
            payload.Text,
            runCorrelation);

        _conversationStore.AppendEntry(agentEvent.ConversationId, entry);
        _projectedMessageEntryIds.Add(payload.MessageEntryId);
    }

    private void ProjectAssistantMessageCompleted(AgentEvent agentEvent)
    {
        if (agentEvent.Payload is not AgentMessagePayload payload)
        {
            return;
        }

        if (_projectedMessageEntryIds.Contains(payload.MessageEntryId)
            || _projectedTerminalRunIds.Contains(agentEvent.RunId))
        {
            return;
        }

        if (!_conversationStore.TryGet(agentEvent.ConversationId, out var conversation))
        {
            return;
        }

        var runCorrelation = ExecutionRunCorrelation.ToEntryCorrelation(agentEvent.RunId);
        if (conversation.Entries.Any(e => e.Id == payload.MessageEntryId
                                         || (e.CorrelationId == runCorrelation && e.Kind == ConversationEntryKind.AssistantResponse)))
        {
            _projectedMessageEntryIds.Add(payload.MessageEntryId);
            _projectedTerminalRunIds.Add(agentEvent.RunId);
            return;
        }

        var authorActorId = ResolveAgentAuthor(conversation);
        var entry = ConversationEntry.AssistantResponse(
            payload.MessageEntryId,
            authorActorId,
            agentEvent.OccurredAtUtc,
            payload.Text,
            runCorrelation);

        _conversationStore.AppendEntry(agentEvent.ConversationId, entry);
        _projectedMessageEntryIds.Add(payload.MessageEntryId);
        _projectedTerminalRunIds.Add(agentEvent.RunId);
    }

    private void ProjectFailureReported(AgentEvent agentEvent)
    {
        if (agentEvent.Payload is not AgentFailurePayload payload)
        {
            return;
        }

        ProjectTerminalFailureEntry(agentEvent, payload.Reason);
    }

    private void ProjectRunTerminalFailure(AgentEvent agentEvent)
    {
        var reason = ResolveFallbackFailureReason(agentEvent);
        ProjectTerminalFailureEntry(agentEvent, reason);
    }

    private void ProjectTerminalFailureEntry(AgentEvent agentEvent, string reason)
    {
        var runCorrelation = ExecutionRunCorrelation.ToEntryCorrelation(agentEvent.RunId);

        if (!_admittedRunIds.Contains(agentEvent.RunId)
            && (_conversationStore.TryGet(agentEvent.ConversationId, out var existingConv)
                && !existingConv.Entries.Any(e => e.CorrelationId == runCorrelation)))
        {
            // Rejections or non-admitted runs do not append conversation entries.
            return;
        }

        if (_projectedTerminalRunIds.Contains(agentEvent.RunId))
        {
            return;
        }

        if (!_conversationStore.TryGet(agentEvent.ConversationId, out var conversation))
        {
            return;
        }

        if (conversation.Entries.Any(e => e.CorrelationId == runCorrelation
                                         && (e.Kind == ConversationEntryKind.AssistantResponse
                                             || e.Kind == ConversationEntryKind.ExecutionFailure
                                             || e.Kind == ConversationEntryKind.RoutingFailure)))
        {
            _projectedTerminalRunIds.Add(agentEvent.RunId);
            return;
        }

        var authorActorId = ResolveAgentAuthor(conversation);
        var entry = ConversationEntry.ExecutionFailure(
            ConversationEntryId.New(),
            authorActorId,
            agentEvent.OccurredAtUtc,
            reason,
            runCorrelation);

        _conversationStore.AppendEntry(agentEvent.ConversationId, entry);
        _projectedTerminalRunIds.Add(agentEvent.RunId);
    }

    private ActorId ResolveAgentAuthor(Conversation conversation)
    {
        var humanId = _actorCatalog?.CanonicalHuman.Id ?? ActorId.HumanUser;
        var peer = conversation.Participants.All.FirstOrDefault(p => p != humanId);
        if (peer != default)
        {
            return peer;
        }

        return _actorCatalog?.CanonicalTownhallAgent.Id ?? ActorId.TownhallAgent;
    }

    private static string ResolveFallbackFailureReason(AgentEvent agentEvent)
    {
        return agentEvent.Kind switch
        {
            AgentEventKind.RunTimedOut => "Request timed out.",
            AgentEventKind.RunCancelled => "The operation was canceled.",
            AgentEventKind.RunDisconnected => "Connection was lost.",
            AgentEventKind.RunIndeterminate => "Request ended indeterminately.",
            _ => "Request failed.",
        };
    }
}
