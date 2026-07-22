using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Orchestrates agent send flow by composing <see cref="IAgentPanelHost"/> and
/// <see cref="IAgentSessionService"/>. Owns per-<see cref="ConversationId"/>
/// one-in-flight enforcement; panel chrome is a thin projection of conversation
/// busy/status/draft. No View, Townhall, or provider-platform references.
/// </summary>
public sealed class AgentExecutionCoordinator : IAgentExecutionCoordinator
{
    private readonly IAgentPanelHost _panelHost;
    private readonly IAgentSessionService _sessionService;
    private readonly AgentBackendId _backendId;
    private readonly IConversationStore _conversationStore;
    private readonly IConversationDraftState? _draftState;
    private readonly Dictionary<ConversationId, ExecutionRunId> _inFlightRuns = new();
    private readonly Dictionary<ConversationId, long> _busyVersions = new();
    private readonly Dictionary<ConversationId, bool> _publishedBusyStates = new();
    private readonly Dictionary<ConversationId, bool> _lastInvokedBusyStates = new();
    private readonly Queue<(ConversationId ConversationId, bool IsBusy, long Version)> _busyNotificationQueue = new();
    private bool _isDrainingBusyNotifications;
    private readonly object _sync = new();

    /// <summary>
    /// Test hook — invoked before terminal panel projection for an admitted run.
    /// </summary>
    internal Func<ConversationId, ExecutionRunId, Task>? OnBeforeTerminalPanelProjectionAsync { get; set; }

    /// <summary>
    /// Test hook — invoked after in-flight removal is committed but before busy
    /// notifications are drained.
    /// </summary>
    internal Func<ConversationId, ExecutionRunId, Task>? OnAfterInFlightRemovalBeforeBusyNotificationAsync
    {
        get;
        set;
    }

    internal AgentExecutionCoordinator(
        IAgentPanelHost panelHost,
        IAgentSessionService sessionService,
        IConversationStore conversationStore,
        IConversationDraftState? draftState)
        : this(panelHost, sessionService, conversationStore, draftState, null)
    {
    }

    internal AgentExecutionCoordinator(
        IAgentPanelHost panelHost,
        IAgentSessionService sessionService,
        IConversationStore conversationStore)
        : this(panelHost, sessionService, conversationStore, null, null)
    {
    }

    private AgentExecutionCoordinator(
        IAgentPanelHost panelHost,
        IAgentSessionService sessionService,
        IConversationStore conversationStore,
        IConversationDraftState? draftState,
        AgentBackendId? backendId)
    {
        _panelHost = panelHost ?? throw new ArgumentNullException(nameof(panelHost));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _conversationStore = conversationStore ?? throw new ArgumentNullException(nameof(conversationStore));
        _draftState = draftState;
        _backendId = backendId ?? AgentBackendIds.LegacyOpenAiCompatible;
    }

    public event Action<ConversationId, bool>? ConversationBusyChanged;

    public bool IsConversationBusy(ConversationId conversationId)
    {
        lock (_sync)
        {
            return _inFlightRuns.ContainsKey(conversationId);
        }
    }

    public async Task<AgentExecutionCoordinatorResult?> SendAsync(
        string panelId,
        string userMessage,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(panelId))
            return null;

        if (string.IsNullOrWhiteSpace(userMessage))
            return null;

        var panel = _panelHost.Panels.FirstOrDefault(p => p.PanelId == panelId);
        if (panel is null)
            return null;

        var conversationId = panel.ConversationId;
        var messageEntryId = ConversationEntryId.New();
        using var capture = new AgentSessionCoordinatorEventCapture(
            conversationId,
            messageEntryId);
        capture.Subscribe(_sessionService.Events);

        var admittedRunId = default(ExecutionRunId?);
        ExecutionRunId? ownedRunId = null;
        Task<AgentRunSnapshot>? sendTask = null;

        try
        {
            sendTask = _sessionService.SendAsync(
                conversationId,
                ActorId.HumanUser,
                panel.ActorId,
                _backendId,
                messageEntryId,
                userMessage,
                ct);

            admittedRunId = await capture
                .WaitForAdmissionOrRejectionAsync(sendTask, ct)
                .ConfigureAwait(false);

            if (admittedRunId is null)
            {
                var rejectedSnapshot = await sendTask.ConfigureAwait(false);
                return CreateSessionRejectionResult(
                    panel,
                    rejectedSnapshot,
                    capture);
            }

            ownedRunId = admittedRunId.Value;
            TryBeginInFlight(conversationId, admittedRunId.Value);
            ApplyPanelBusyProjection(
                conversationId,
                isBusy: true,
                status: "Thinking",
                owningRunId: admittedRunId.Value);
            ClearDraft(panel);

            var snapshot = await sendTask.WaitAsync(ct).ConfigureAwait(false);
            return await CreateTerminalResultAsync(
                panel,
                snapshot,
                capture.GetEventsForRun(snapshot.RunId)).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            var (result, acquiredOwnership) = await HandleCallerCancellationAsync(
                    panel,
                    userMessage,
                    messageEntryId,
                    sendTask,
                    capture,
                    admittedRunId)
                .ConfigureAwait(false);

            ownedRunId ??= acquiredOwnership;
            return result;
        }
        finally
        {
            if (ownedRunId is { } runId)
            {
                await TryEndInFlightAsync(conversationId, runId).ConfigureAwait(false);
            }
        }
    }

    private async Task<(AgentExecutionCoordinatorResult result, ExecutionRunId? acquiredOwnership)>
        HandleCallerCancellationAsync(
        AgentPanelState panel,
        string userMessage,
        ConversationEntryId messageEntryId,
        Task<AgentRunSnapshot>? sendTask,
        AgentSessionCoordinatorEventCapture capture,
        ExecutionRunId? admittedRunId)
    {
        await _sessionService
            .CancelAsync(panel.ConversationId, CancellationToken.None)
            .ConfigureAwait(false);

        if (sendTask is null)
        {
            throw new InvalidOperationException(
                "Session send was not started before caller cancellation.");
        }

        var snapshot = await AwaitSessionSendSnapshotAsync(sendTask).ConfigureAwait(false);
        var resolvedRunId = admittedRunId ?? capture.TryGetAdmittedRunId();

        if (snapshot.Status == AgentRunStatus.Rejected)
        {
            return (CreateSessionRejectionResult(panel, snapshot, capture), null);
        }

        if (!AgentRunStatusTransitions.IsTerminal(snapshot.Status))
        {
            throw new InvalidOperationException(
                $"Session send returned nonterminal status '{snapshot.Status}' after caller cancellation.");
        }

        if (resolvedRunId is not null)
        {
            var acquiredOwnership = EnsureInFlightOwnership(panel.ConversationId, resolvedRunId.Value)
                ? resolvedRunId
                : null;
            return (
                await FinalizeAdmittedTerminalResultAsync(
                    panel,
                    userMessage,
                    messageEntryId,
                    snapshot,
                    capture,
                    resolvedRunId.Value).ConfigureAwait(false),
                acquiredOwnership);
        }

        return (
            await CreateTerminalResultAsync(
                panel,
                snapshot,
                capture.GetEventsForRun(snapshot.RunId)).ConfigureAwait(false),
            null);
    }

    private static async Task<AgentRunSnapshot> AwaitSessionSendSnapshotAsync(
        Task<AgentRunSnapshot> sendTask)
    {
        try
        {
            return await sendTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return await sendTask.ConfigureAwait(false);
        }
    }

    private async Task<AgentExecutionCoordinatorResult> FinalizeAdmittedTerminalResultAsync(
        AgentPanelState panel,
        string userMessage,
        ConversationEntryId messageEntryId,
        AgentRunSnapshot snapshot,
        AgentSessionCoordinatorEventCapture capture,
        ExecutionRunId admittedRunId)
    {
        EnsureInFlightOwnership(panel.ConversationId, admittedRunId);
        ApplyPanelBusyProjection(
            panel.ConversationId,
            isBusy: true,
            status: "Thinking",
            owningRunId: admittedRunId);
        ClearDraft(panel);

        return await CreateTerminalResultAsync(
            panel,
            snapshot,
            capture.GetEventsForRun(snapshot.RunId)).ConfigureAwait(false);
    }

    private async Task InvokeBeforeTerminalPanelProjectionHookAsync(
        ConversationId conversationId,
        ExecutionRunId runId)
    {
        if (OnBeforeTerminalPanelProjectionAsync is not { } hook)
        {
            return;
        }

        await hook(conversationId, runId).ConfigureAwait(false);
    }

    private AgentExecutionCoordinatorResult CreateSessionRejectionResult(
        AgentPanelState panel,
        AgentRunSnapshot snapshot,
        AgentSessionCoordinatorEventCapture capture)
    {
        var reason = ExtractFailureReason(
            capture.GetEventsForRun(snapshot.RunId),
            snapshot.RunId,
            snapshot.Status,
            fallback: "Admission was rejected.");

        var resultPanelId = FindPanelForConversation(panel.ConversationId)?.PanelId ?? panel.PanelId;
        var run = new ExecutionRun(
            snapshot.RunId,
            panel.ConversationId,
            ActorId.HumanUser,
            panel.ActorId,
            resultPanelId,
            ExecutionRunOutcome.Rejected);

        return AgentExecutionCoordinatorResult.Rejected(run, reason);
    }

    private async Task<AgentExecutionCoordinatorResult> CreateTerminalResultAsync(
        AgentPanelState panel,
        AgentRunSnapshot snapshot,
        IReadOnlyList<AgentEvent> runEvents)
    {
        await InvokeBeforeTerminalPanelProjectionHookAsync(
            panel.ConversationId,
            snapshot.RunId).ConfigureAwait(false);

        var outcome = MapRunStatus(snapshot.Status);
        var resultPanelId = FindPanelForConversation(panel.ConversationId)?.PanelId ?? panel.PanelId;
        var run = new ExecutionRun(
            snapshot.RunId,
            panel.ConversationId,
            ActorId.HumanUser,
            panel.ActorId,
            resultPanelId,
            outcome);

        switch (outcome)
        {
            case ExecutionRunOutcome.Success:
            {
                var assistantText = ExtractAssistantText(runEvents, snapshot.RunId);
                if (string.IsNullOrWhiteSpace(assistantText))
                {
                    const string emptyMessage = "Assistant response was empty.";
                    ApplyPanelBusyProjection(
                        panel.ConversationId,
                        isBusy: false,
                        status: "Error",
                        owningRunId: snapshot.RunId);

                    var failureRun = new ExecutionRun(
                        snapshot.RunId,
                        panel.ConversationId,
                        ActorId.HumanUser,
                        panel.ActorId,
                        resultPanelId,
                        ExecutionRunOutcome.ExecutionFailure);
                    return AgentExecutionCoordinatorResult.Failure(failureRun, emptyMessage);
                }

                ApplyPanelBusyProjection(
                    panel.ConversationId,
                    isBusy: false,
                    status: "Idle",
                    owningRunId: snapshot.RunId);
                return AgentExecutionCoordinatorResult.Success(run, assistantText);
            }

            case ExecutionRunOutcome.Cancelled:
            case ExecutionRunOutcome.ExecutionFailure:
            {
                var errorMessage = ExtractFailureReason(
                    runEvents,
                    snapshot.RunId,
                    snapshot.Status,
                    fallback: "Request failed.");
                ApplyPanelBusyProjection(
                    panel.ConversationId,
                    isBusy: false,
                    status: "Error",
                    owningRunId: snapshot.RunId);
                return AgentExecutionCoordinatorResult.Failure(run, errorMessage);
            }

            case ExecutionRunOutcome.Rejected:
            {
                var rejectionReason = ExtractFailureReason(
                    runEvents,
                    snapshot.RunId,
                    snapshot.Status,
                    fallback: "Admission was rejected.");
                ApplyPanelBusyProjection(
                    panel.ConversationId,
                    isBusy: false,
                    status: "Idle",
                    owningRunId: snapshot.RunId);
                return AgentExecutionCoordinatorResult.Rejected(run, rejectionReason);
            }

            default:
                throw new InvalidOperationException(
                    $"Unexpected coordinator outcome mapping for run status '{snapshot.Status}'.");
        }
    }

    private static ExecutionRunOutcome MapRunStatus(AgentRunStatus status) =>
        status switch
        {
            AgentRunStatus.Completed => ExecutionRunOutcome.Success,
            AgentRunStatus.Cancelled => ExecutionRunOutcome.Cancelled,
            AgentRunStatus.Rejected => ExecutionRunOutcome.Rejected,
            AgentRunStatus.Failed
                or AgentRunStatus.TimedOut
                or AgentRunStatus.Disconnected
                or AgentRunStatus.Indeterminate => ExecutionRunOutcome.ExecutionFailure,
            _ => throw new InvalidOperationException(
                $"Unsupported terminal run status '{status}'."),
        };

    private static string? ExtractAssistantText(
        IReadOnlyList<AgentEvent> runEvents,
        ExecutionRunId runId)
    {
        for (var index = runEvents.Count - 1; index >= 0; index--)
        {
            var agentEvent = runEvents[index];
            if (agentEvent.RunId != runId
                || agentEvent.Kind != AgentEventKind.AssistantMessageCompleted
                || agentEvent.Payload is not AgentMessagePayload payload)
            {
                continue;
            }

            return payload.Text;
        }

        return null;
    }

    private static string ExtractFailureReason(
        IReadOnlyList<AgentEvent> runEvents,
        ExecutionRunId runId,
        AgentRunStatus status,
        string fallback)
    {
        for (var index = runEvents.Count - 1; index >= 0; index--)
        {
            var agentEvent = runEvents[index];
            if (agentEvent.RunId != runId
                || agentEvent.Kind != AgentEventKind.FailureReported
                || agentEvent.Payload is not AgentFailurePayload payload)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(payload.Reason))
            {
                return payload.Reason;
            }
        }

        return status switch
        {
            AgentRunStatus.TimedOut => "Request timed out.",
            AgentRunStatus.Disconnected => "Connection was lost.",
            AgentRunStatus.Indeterminate => "Request ended indeterminately.",
            AgentRunStatus.Cancelled => "The operation was canceled.",
            AgentRunStatus.Rejected => fallback,
            _ => fallback,
        };
    }

    private bool TryBeginInFlight(ConversationId conversationId, ExecutionRunId runId)
    {
        lock (_sync)
        {
            var wasBusy = _inFlightRuns.ContainsKey(conversationId);
            if (_inFlightRuns.TryGetValue(conversationId, out var existingOwner))
            {
                if (existingOwner == runId)
                {
                    return true;
                }

                _inFlightRuns[conversationId] = runId;
            }
            else
            {
                _inFlightRuns[conversationId] = runId;
            }

            var isBusy = _inFlightRuns.ContainsKey(conversationId);
            if (!wasBusy && isBusy)
            {
                EnqueueBusyNotificationLocked(conversationId, isBusy: true);
            }
        }

        DrainBusyNotifications();
        return true;
    }

    private bool EnsureInFlightOwnership(ConversationId conversationId, ExecutionRunId runId) =>
        TryBeginInFlight(conversationId, runId);

    private async Task<bool> TryEndInFlightAsync(ConversationId conversationId, ExecutionRunId runId)
    {
        var removed = false;
        lock (_sync)
        {
            if (!_inFlightRuns.TryGetValue(conversationId, out var owner)
                || owner != runId)
            {
                return false;
            }

            _inFlightRuns.Remove(conversationId);
            removed = true;

            if (!_inFlightRuns.ContainsKey(conversationId))
            {
                EnqueueBusyNotificationLocked(conversationId, isBusy: false);
            }
        }

        if (removed
            && OnAfterInFlightRemovalBeforeBusyNotificationAsync is not null)
        {
            await OnAfterInFlightRemovalBeforeBusyNotificationAsync
                .Invoke(conversationId, runId)
                .ConfigureAwait(false);
        }

        DrainBusyNotifications();
        return removed;
    }

    private void EnqueueBusyNotificationLocked(ConversationId conversationId, bool isBusy)
    {
        var version = _busyVersions.GetValueOrDefault(conversationId) + 1;
        _busyVersions[conversationId] = version;
        _publishedBusyStates[conversationId] = isBusy;
        _busyNotificationQueue.Enqueue((conversationId, isBusy, version));
    }

    private void DrainBusyNotifications()
    {
        lock (_sync)
        {
            if (_isDrainingBusyNotifications)
            {
                return;
            }

            _isDrainingBusyNotifications = true;
        }

        try
        {
            while (TryDequeueBusyNotification(out var notification))
            {
                if (!TryPrepareBusyNotificationInvocation(notification, out var conversationId, out var isBusy))
                {
                    continue;
                }

                InvokeConversationBusyChanged(conversationId, isBusy);
                CommitBusyNotificationInvocation(conversationId, isBusy);
            }
        }
        finally
        {
            var resumeDrain = false;
            lock (_sync)
            {
                _isDrainingBusyNotifications = false;
                resumeDrain = _busyNotificationQueue.Count > 0;
            }

            if (resumeDrain)
            {
                DrainBusyNotifications();
            }
        }
    }

    private bool TryDequeueBusyNotification(
        out (ConversationId ConversationId, bool IsBusy, long Version) notification)
    {
        lock (_sync)
        {
            if (_busyNotificationQueue.Count == 0)
            {
                notification = default;
                return false;
            }

            notification = _busyNotificationQueue.Dequeue();
            return true;
        }
    }

    private bool TryPrepareBusyNotificationInvocation(
        (ConversationId ConversationId, bool IsBusy, long Version) notification,
        out ConversationId conversationId,
        out bool isBusy)
    {
        lock (_sync)
        {
            conversationId = notification.ConversationId;
            isBusy = notification.IsBusy;

            if (!_busyVersions.TryGetValue(conversationId, out var currentVersion)
                || currentVersion != notification.Version
                || !_publishedBusyStates.TryGetValue(conversationId, out var publishedBusy)
                || publishedBusy != isBusy)
            {
                return false;
            }

            return !_lastInvokedBusyStates.TryGetValue(conversationId, out var lastInvoked)
                || lastInvoked != isBusy;
        }
    }

    private void CommitBusyNotificationInvocation(ConversationId conversationId, bool isBusy)
    {
        lock (_sync)
        {
            _lastInvokedBusyStates[conversationId] = isBusy;
        }
    }

    private void InvokeConversationBusyChanged(ConversationId conversationId, bool isBusy)
    {
        var handlers = ConversationBusyChanged;
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<ConversationId, bool>)handler).Invoke(conversationId, isBusy);
            }
            catch (Exception)
            {
            }
        }
    }

    private void ApplyPanelBusyProjection(
        ConversationId conversationId,
        bool isBusy,
        string status,
        ExecutionRunId? owningRunId = null)
    {
        lock (_sync)
        {
            if (owningRunId is { } runId)
            {
                if (!_inFlightRuns.TryGetValue(conversationId, out var owner)
                    || owner != runId)
                {
                    return;
                }
            }

            var livePanel = FindPanelForConversation(conversationId);
            if (livePanel is null)
            {
                return;
            }

            livePanel.Status = status;
            livePanel.IsBusy = isBusy;
        }
    }

    private AgentPanelState? FindPanelForConversation(ConversationId conversationId) =>
        _panelHost.Panels.FirstOrDefault(p => p.ConversationId == conversationId);

    private void ClearDraft(AgentPanelState panel)
    {
        panel.DraftInput = string.Empty;
        _draftState?.ClearDraft(panel.ConversationId);
    }
}
