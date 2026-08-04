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
    internal const string RouteStatusContentPrefix = "zaide-route|v1|";
    internal const string CancellationIntentContentPrefix = "zaide-cancellation-intent|v1|";
    internal const string SessionEndingContentPrefix = "zaide-session-ending|v1|";
    internal const string SessionEndedContentPrefix = "zaide-session-ended|v1|";
    internal const string TerminationIndeterminateContentPrefix = "zaide-termination-indeterminate|v1|";
    internal const string LateCompletionLabelPrefix = "zaide-late-completion|v1|";

    private readonly IConversationStore _conversationStore;
    private readonly IActorCatalog? _actorCatalog;
    private readonly IDisposable? _subscription;
    private readonly object _sync = new();

    private readonly HashSet<ConversationEntryId> _projectedMessageEntryIds = new();
    private readonly HashSet<ExecutionRunId> _admittedRunIds = new();
    private readonly HashSet<ExecutionRunId> _projectedTerminalRunIds = new();
    private readonly HashSet<ExecutionRunId> _projectedRejectionRunIds = new();
    private readonly HashSet<ExecutionRunId> _projectedCancellationIntentRunIds = new();
    private readonly HashSet<AgentSessionId> _projectedSessionEndingIds = new();
    private readonly HashSet<AgentSessionId> _projectedSessionEndedIds = new();
    private readonly HashSet<AgentActionId> _projectedActionSummaryIds = new();
    private readonly HashSet<string> _projectedBackendActivityKeys = new();

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

    public static ConversationEntry ProjectAdmissionRejection(
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
            && conversation.Entries.Any(e => e.CorrelationId == runCorrelation
                                             && e.Kind == ConversationEntryKind.ExecutionFailure))
        {
            return conversation.Entries.First(e => e.CorrelationId == runCorrelation
                                                   && e.Kind == ConversationEntryKind.ExecutionFailure);
        }

        var entry = ConversationEntry.ExecutionFailure(
            ConversationEntryId.New(),
            author,
            DateTimeOffset.UtcNow,
            failureReason,
            runCorrelation);

        conversationStore.AppendEntry(conversationId, entry);
        return entry;
    }

    public static ConversationEntry ProjectRouteStatus(
        IConversationStore conversationStore,
        ConversationId sourceConversationId,
        ActorId author,
        ExecutionRunId runId,
        ActorId targetActorId,
        ConversationId targetConversationId,
        string targetDisplayName,
        string outcome)
    {
        ArgumentNullException.ThrowIfNull(conversationStore);
        if (sourceConversationId == default)
        {
            throw new ArgumentException("Source conversation id is required.", nameof(sourceConversationId));
        }

        if (author == default)
        {
            throw new ArgumentException("Author is required.", nameof(author));
        }

        if (runId == default)
        {
            throw new ArgumentException("Run id is required.", nameof(runId));
        }

        if (targetActorId == default)
        {
            throw new ArgumentException("Target actor id is required.", nameof(targetActorId));
        }

        if (targetConversationId == default)
        {
            throw new ArgumentException("Target conversation id is required.", nameof(targetConversationId));
        }

        if (string.IsNullOrWhiteSpace(targetDisplayName))
        {
            throw new ArgumentException("Target display name is required.", nameof(targetDisplayName));
        }

        if (string.IsNullOrWhiteSpace(outcome))
        {
            throw new ArgumentException("Route outcome is required.", nameof(outcome));
        }

        var runCorrelation = ExecutionRunCorrelation.ToEntryCorrelation(runId);
        var content = FormatRouteStatusContent(
            outcome,
            targetActorId,
            targetConversationId,
            targetDisplayName);

        if (conversationStore.TryGet(sourceConversationId, out var conversation)
            && conversation.Entries.Any(e => e.CorrelationId == runCorrelation
                                             && e.Kind == ConversationEntryKind.SystemNotification
                                             && e.Content == content))
        {
            return conversation.Entries.First(e => e.CorrelationId == runCorrelation
                                                 && e.Kind == ConversationEntryKind.SystemNotification
                                                 && e.Content == content);
        }

        var entry = ConversationEntry.SystemNotification(
            ConversationEntryId.New(),
            author,
            DateTimeOffset.UtcNow,
            content,
            runCorrelation);

        conversationStore.AppendEntry(sourceConversationId, entry);
        return entry;
    }

    internal static string FormatRouteStatusContent(
        string outcome,
        ActorId targetActorId,
        ConversationId targetConversationId,
        string targetDisplayName) =>
        string.Join(
            '|',
            "zaide-route",
            "v1",
            outcome,
            targetActorId.Value,
            targetConversationId.Value,
            targetDisplayName);

    internal static string FormatCancellationIntentContent() =>
        string.Join('|', "zaide-cancellation-intent", "v1", "Cancellation requested.");

    internal static string FormatSessionEndingContent() =>
        string.Join('|', "zaide-session-ending", "v1", "Session ending.");

    internal static string FormatSessionEndedContent() =>
        string.Join(
            '|',
            "zaide-session-ended",
            "v1",
            "Session ended. Live ownership removed. Provider termination is not claimed.");

    internal static string FormatTerminationIndeterminateContent(string reason) =>
        string.Join(
            '|',
            "zaide-termination-indeterminate",
            "v1",
            string.IsNullOrWhiteSpace(reason)
                ? "Backend acknowledgement timed out. Retry is available. Provider termination is not claimed."
                : reason.Replace('|', '/'));

    internal static string FormatLateCompletionContent(string assistantText) =>
        string.Join(
            '|',
            "zaide-late-completion",
            "v1",
            string.IsNullOrWhiteSpace(assistantText) ? "(empty)" : assistantText.Replace('|', '/'));

    /// <summary>
    /// Projects a user-visible indeterminate termination result into the owning conversation.
    /// Exactly once per correlated termination attempt. Distinct attempts/sessions each get
    /// their own entry. Raw correlation identifiers must not appear in <paramref name="reason"/>.
    /// </summary>
    public static ConversationEntry ProjectTerminationIndeterminate(
        IConversationStore conversationStore,
        ConversationId conversationId,
        ActorId authorActorId,
        string reason,
        ConversationEntryCorrelationId? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(conversationStore);
        if (conversationId == default)
        {
            throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        }

        if (authorActorId == default)
        {
            throw new ArgumentException("Author actor id is required.", nameof(authorActorId));
        }

        var content = FormatTerminationIndeterminateContent(reason);
        if (conversationStore.TryGet(conversationId, out var conversation))
        {
            ConversationEntry? existing = null;
            foreach (var entryCandidate in conversation.Entries)
            {
                if (entryCandidate.Kind != ConversationEntryKind.SystemNotification
                    || !entryCandidate.Content.StartsWith(
                        TerminationIndeterminateContentPrefix,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (correlationId is { } required
                    && entryCandidate.CorrelationId == required)
                {
                    existing = entryCandidate;
                    break;
                }

                if (correlationId is null && entryCandidate.CorrelationId is null)
                {
                    existing = entryCandidate;
                    break;
                }
            }

            if (existing is not null)
            {
                return existing;
            }
        }

        var entry = ConversationEntry.SystemNotification(
            ConversationEntryId.New(),
            authorActorId,
            DateTimeOffset.UtcNow,
            content,
            correlationId);

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
                    // Admission rejection reason is projected from FailureReported.
                    break;

                case AgentEventKind.RunCancellationRequested:
                    ProjectRunCancellationRequested(agentEvent);
                    break;

                case AgentEventKind.SessionEnding:
                    ProjectSessionEnding(agentEvent);
                    break;

                case AgentEventKind.SessionEnded:
                    ProjectSessionEnded(agentEvent);
                    break;

                case AgentEventKind.ActionResultReported:
                    ProjectActionResultReported(agentEvent);
                    break;

                case AgentEventKind.BackendActivityReported:
                    ProjectBackendActivityReported(agentEvent);
                    break;
            }
        }
    }

    private void ProjectRunCancellationRequested(AgentEvent agentEvent)
    {
        if (!_admittedRunIds.Contains(agentEvent.RunId)
            || _projectedCancellationIntentRunIds.Contains(agentEvent.RunId))
        {
            return;
        }

        if (!_conversationStore.TryGet(agentEvent.ConversationId, out var conversation))
        {
            return;
        }

        var runCorrelation = ExecutionRunCorrelation.ToEntryCorrelation(agentEvent.RunId);
        if (conversation.Entries.Any(e => e.CorrelationId == runCorrelation
                                         && e.Kind == ConversationEntryKind.SystemNotification
                                         && e.Content.StartsWith(CancellationIntentContentPrefix, StringComparison.Ordinal)))
        {
            _projectedCancellationIntentRunIds.Add(agentEvent.RunId);
            return;
        }

        var authorActorId = ResolveAgentAuthor(conversation);
        var entry = ConversationEntry.SystemNotification(
            ConversationEntryId.New(),
            authorActorId,
            agentEvent.OccurredAtUtc,
            FormatCancellationIntentContent(),
            runCorrelation);

        _conversationStore.AppendEntry(agentEvent.ConversationId, entry);
        _projectedCancellationIntentRunIds.Add(agentEvent.RunId);
    }

    private void ProjectSessionEnding(AgentEvent agentEvent)
    {
        if (_projectedSessionEndingIds.Contains(agentEvent.SessionId))
        {
            return;
        }

        if (!_conversationStore.TryGet(agentEvent.ConversationId, out var conversation))
        {
            return;
        }

        var runCorrelation = ExecutionRunCorrelation.ToEntryCorrelation(agentEvent.RunId);
        if (conversation.Entries.Any(e =>
                e.CorrelationId == runCorrelation
                && e.Kind == ConversationEntryKind.SystemNotification
                && e.Content.StartsWith(SessionEndingContentPrefix, StringComparison.Ordinal)))
        {
            _projectedSessionEndingIds.Add(agentEvent.SessionId);
            return;
        }

        var authorActorId = ResolveAgentAuthor(conversation);
        var entry = ConversationEntry.SystemNotification(
            ConversationEntryId.New(),
            authorActorId,
            agentEvent.OccurredAtUtc,
            FormatSessionEndingContent(),
            runCorrelation);

        _conversationStore.AppendEntry(agentEvent.ConversationId, entry);
        _projectedSessionEndingIds.Add(agentEvent.SessionId);
    }

    private void ProjectSessionEnded(AgentEvent agentEvent)
    {
        if (_projectedSessionEndedIds.Contains(agentEvent.SessionId))
        {
            return;
        }

        if (!_conversationStore.TryGet(agentEvent.ConversationId, out var conversation))
        {
            return;
        }

        var runCorrelation = ExecutionRunCorrelation.ToEntryCorrelation(agentEvent.RunId);
        if (conversation.Entries.Any(e =>
                e.CorrelationId == runCorrelation
                && e.Kind == ConversationEntryKind.SystemNotification
                && e.Content.StartsWith(SessionEndedContentPrefix, StringComparison.Ordinal)))
        {
            _projectedSessionEndedIds.Add(agentEvent.SessionId);
            return;
        }

        var authorActorId = ResolveAgentAuthor(conversation);
        var entry = ConversationEntry.SystemNotification(
            ConversationEntryId.New(),
            authorActorId,
            agentEvent.OccurredAtUtc,
            FormatSessionEndedContent(),
            runCorrelation);

        _conversationStore.AppendEntry(agentEvent.ConversationId, entry);
        _projectedSessionEndedIds.Add(agentEvent.SessionId);
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
        var content = FormatActionResultEntryContent(
            payload.ActionKind,
            payload.ResultKind.Value,
            agentEvent.EvidenceLevel,
            payload.Summary);
        var entry = ConversationEntry.SystemNotification(
            ConversationEntryId.New(),
            authorActorId,
            agentEvent.OccurredAtUtc,
            content,
            runCorrelation);

        _conversationStore.AppendEntry(agentEvent.ConversationId, entry);
        _projectedActionSummaryIds.Add(payload.ActionId);
    }

    private void ProjectBackendActivityReported(AgentEvent agentEvent)
    {
        if (agentEvent.Payload is not AgentBackendReportedActivityPayload payload)
        {
            return;
        }

        var dedupeKey = string.Join(
            '|',
            agentEvent.RunId.Value,
            payload.ActivityKind.ToString(),
            payload.AcpCorrelationId ?? string.Empty,
            payload.Summary);
        if (_projectedBackendActivityKeys.Contains(dedupeKey))
        {
            return;
        }

        if (!_conversationStore.TryGet(agentEvent.ConversationId, out var conversation))
        {
            return;
        }

        var runCorrelation = ExecutionRunCorrelation.ToEntryCorrelation(agentEvent.RunId);
        var authorActorId = ResolveAgentAuthor(conversation);
        var content = FormatBackendActivityEntryContent(
            payload.ActivityKind,
            agentEvent.EvidenceLevel,
            payload.Summary,
            payload.AcpCorrelationId);
        var entry = ConversationEntry.SystemNotification(
            ConversationEntryId.New(),
            authorActorId,
            agentEvent.OccurredAtUtc,
            content,
            runCorrelation);

        _conversationStore.AppendEntry(agentEvent.ConversationId, entry);
        _projectedBackendActivityKeys.Add(dedupeKey);
    }

    internal static string FormatBackendActivityEntryContent(
        AcpBackendActivityKind activityKind,
        AgentActivityEvidenceLevel evidenceLevel,
        string summary,
        string? acpCorrelationId)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("Activity summary is required.", nameof(summary));
        }

        return string.Join(
            '|',
            "zaide-backend-activity",
            "v1",
            activityKind.ToString(),
            evidenceLevel.ToString(),
            acpCorrelationId ?? string.Empty,
            summary);
    }

    internal static string FormatActionResultEntryContent(
        AgentActionKind actionKind,
        AgentActionResultKind resultKind,
        AgentActivityEvidenceLevel evidenceLevel,
        AgentActionAuditSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        var headline = ResolveActionHeadline(actionKind);
        return string.Join(
            '|',
            "zaide-action",
            "v1",
            actionKind.ToString(),
            headline,
            resultKind.ToString(),
            evidenceLevel.ToString(),
            summary.WasTruncated ? "1" : "0",
            summary.WasRedacted ? "1" : "0",
            summary.Text);
    }

    internal static string ResolveActionHeadline(AgentActionKind actionKind) =>
        actionKind switch
        {
            AgentActionKind.ReadFile => "Read file",
            AgentActionKind.CreateFile => "Create file",
            AgentActionKind.ReplaceFile => "Replace file",
            AgentActionKind.DeleteFile => "Delete file",
            AgentActionKind.ExecuteCommand => "Run command",
            _ => "Agent action",
        };

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

        if (_projectedMessageEntryIds.Contains(payload.MessageEntryId))
        {
            return;
        }

        if (_projectedTerminalRunIds.Contains(agentEvent.RunId)
            && !_projectedCancellationIntentRunIds.Contains(agentEvent.RunId))
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
        var isLateAfterCancellation =
            _projectedCancellationIntentRunIds.Contains(agentEvent.RunId);

        // Late completion after cancellation intent is retained as assistant content
        // and labelled with a separate system notification; never silently overwrites
        // the prior cancellation-intent entry.
        var entry = ConversationEntry.AssistantResponse(
            payload.MessageEntryId,
            authorActorId,
            agentEvent.OccurredAtUtc,
            payload.Text,
            runCorrelation);

        _conversationStore.AppendEntry(agentEvent.ConversationId, entry);
        _projectedMessageEntryIds.Add(payload.MessageEntryId);
        _projectedTerminalRunIds.Add(agentEvent.RunId);

        if (isLateAfterCancellation)
        {
            var labelEntry = ConversationEntry.SystemNotification(
                ConversationEntryId.New(),
                authorActorId,
                agentEvent.OccurredAtUtc,
                FormatLateCompletionContent(payload.Text),
                runCorrelation);
            _conversationStore.AppendEntry(agentEvent.ConversationId, labelEntry);
        }
    }

    private void ProjectFailureReported(AgentEvent agentEvent)
    {
        if (agentEvent.Payload is not AgentFailurePayload payload)
        {
            return;
        }

        if (_admittedRunIds.Contains(agentEvent.RunId))
        {
            ProjectTerminalFailureEntry(agentEvent, payload.Reason);
            return;
        }

        ProjectRejectionFailureEntry(agentEvent, payload.Reason);
    }

    private void ProjectRunTerminalFailure(AgentEvent agentEvent)
    {
        if (HasAssistantResponseForRun(agentEvent.ConversationId, agentEvent.RunId))
        {
            _projectedTerminalRunIds.Add(agentEvent.RunId);
            return;
        }

        var reason = ResolveFallbackFailureReason(agentEvent);
        ProjectTerminalFailureEntry(agentEvent, reason);
    }

    private bool HasAssistantResponseForRun(ConversationId conversationId, ExecutionRunId runId)
    {
        if (!_conversationStore.TryGet(conversationId, out var conversation))
        {
            return false;
        }

        var runCorrelation = ExecutionRunCorrelation.ToEntryCorrelation(runId);
        return conversation.Entries.Any(e => e.CorrelationId == runCorrelation
                                           && e.Kind == ConversationEntryKind.AssistantResponse);
    }

    private void ProjectRejectionFailureEntry(AgentEvent agentEvent, string reason)
    {
        if (_projectedRejectionRunIds.Contains(agentEvent.RunId))
        {
            return;
        }

        if (!_conversationStore.TryGet(agentEvent.ConversationId, out var conversation))
        {
            return;
        }

        var runCorrelation = ExecutionRunCorrelation.ToEntryCorrelation(agentEvent.RunId);
        if (conversation.Entries.Any(e => e.CorrelationId == runCorrelation
                                         && e.Kind == ConversationEntryKind.ExecutionFailure))
        {
            _projectedRejectionRunIds.Add(agentEvent.RunId);
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
        _projectedRejectionRunIds.Add(agentEvent.RunId);
    }

    private void ProjectTerminalFailureEntry(AgentEvent agentEvent, string reason)
    {
        var runCorrelation = ExecutionRunCorrelation.ToEntryCorrelation(agentEvent.RunId);

        if (!_admittedRunIds.Contains(agentEvent.RunId)
            && (_conversationStore.TryGet(agentEvent.ConversationId, out var existingConv)
                && !existingConv.Entries.Any(e => e.CorrelationId == runCorrelation)))
        {
            return;
        }

        if (_projectedTerminalRunIds.Contains(agentEvent.RunId))
        {
            return;
        }

        if (HasAssistantResponseForRun(agentEvent.ConversationId, agentEvent.RunId))
        {
            _projectedTerminalRunIds.Add(agentEvent.RunId);
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
