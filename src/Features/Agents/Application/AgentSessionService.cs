using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Contracts;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Application-owned in-memory Agent Session and run lifecycle coordinator.
/// </summary>
internal sealed class AgentSessionService : IAgentSessionService, IAgentContextSessionPolicyService, IDisposable
{
    private readonly IReadOnlyDictionary<AgentBackendId, IAgentBackend> _backends;
    private readonly AgentEventStream _eventStream;
    private readonly IAgentActionBrokerFactory? _brokerFactory;
    private readonly IAgentActionAuditStore? _auditStore;
    private readonly IWorkspaceActionAuthority? _workspaceAuthority;
    private readonly AgentContextManifestBuilder? _contextManifestBuilder;
    private readonly IAgentContextSnapshotSources? _contextSnapshotSources;
    private readonly Dictionary<ConversationId, LiveSession> _sessions = new();
    private readonly Dictionary<ConversationId, AgentContextPolicyLevel> _sessionPolicyOverrides = new();
    private readonly object _sessionsSync = new();
    private bool _disposed;

    public AgentSessionService(
        IEnumerable<IAgentBackend> backends,
        AgentEventStream eventStream,
        IAgentActionBrokerFactory? brokerFactory = null,
        IAgentActionAuditStore? auditStore = null,
        IWorkspaceActionAuthority? workspaceAuthority = null,
        AgentContextManifestBuilder? contextManifestBuilder = null,
        IAgentContextSnapshotSources? contextSnapshotSources = null)
    {
        ArgumentNullException.ThrowIfNull(backends);
        ArgumentNullException.ThrowIfNull(eventStream);

        _backends = backends.ToDictionary(backend => backend.BackendId);
        _eventStream = eventStream;
        _brokerFactory = brokerFactory;
        _auditStore = auditStore;
        _workspaceAuthority = workspaceAuthority;
        _contextManifestBuilder = contextManifestBuilder;
        _contextSnapshotSources = contextSnapshotSources;

        if (_workspaceAuthority is not null)
        {
            _workspaceAuthority.ScopeInvalidated += OnWorkspaceScopeInvalidated;
        }
    }

    public IObservable<AgentEvent> Events => _eventStream.Events;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_workspaceAuthority is not null)
        {
            _workspaceAuthority.ScopeInvalidated -= OnWorkspaceScopeInvalidated;
        }

        lock (_sessionsSync)
        {
            foreach (var session in _sessions.Values)
            {
                var activeRun = session.ActiveRun;
                if (activeRun is not null
                    && !activeRun.StateMachine.IsTerminal
                    && activeRun.StateMachine.Status != AgentRunStatus.CancellationRequested)
                {
                    activeRun.StateMachine.TransitionTo(AgentRunStatus.CancellationRequested);
                    RevokeRunBrokerLocked(activeRun);
                    activeRun.ExecutionCancellation.Cancel();
                }
                else
                {
                    RevokeRunBrokerLocked(activeRun);
                }
            }

            _sessions.Clear();
        }
    }

    public async Task<AgentRunSnapshot> SendAsync(
        ConversationId conversationId,
        ActorId initiatorActorId,
        ActorId targetActorId,
        AgentBackendId backendId,
        ConversationEntryId messageEntryId,
        string messageText,
        CancellationToken cancellationToken = default)
    {
        if (conversationId == default)
        {
            throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        }

        if (initiatorActorId == default)
        {
            throw new ArgumentException("Initiator actor id is required.", nameof(initiatorActorId));
        }

        if (targetActorId == default)
        {
            throw new ArgumentException("Target actor id is required.", nameof(targetActorId));
        }

        if (backendId == default)
        {
            throw new ArgumentException("Backend id is required.", nameof(backendId));
        }

        if (messageEntryId == default)
        {
            throw new ArgumentException("Message entry id is required.", nameof(messageEntryId));
        }

        if (string.IsNullOrWhiteSpace(messageText))
        {
            throw new ArgumentException("Message text is required.", nameof(messageText));
        }

        if (!_backends.TryGetValue(backendId, out var backend))
        {
            return CreateImmediateRejection(
                conversationId,
                targetActorId,
                backendId,
                "Backend is not registered.");
        }

        LiveSession session;
        LiveRun run;
        var isNewSession = false;

        lock (_sessionsSync)
        {
            session = GetOrCreateSessionLocked(
                conversationId,
                targetActorId,
                backend,
                out isNewSession);

            if (session.SessionState.Status is AgentSessionStatus.Ending or AgentSessionStatus.Ended)
            {
                return RejectRunLocked(
                    session,
                    "Agent session is not accepting new runs.");
            }

            if (session.AgentIdentity != targetActorId)
            {
                return RejectRunLocked(
                    session,
                    "Target actor does not match the bound agent identity.");
            }

            if (session.BackendId != backendId)
            {
                return RejectRunLocked(
                    session,
                    "Backend id does not match the bound backend identity.");
            }

            if (session.ActiveRun is not null)
            {
                return RejectRunLocked(
                    session,
                    "An active run is already in progress for this conversation.");
            }

            run = BeginAdmittedRunLocked(
                session,
                messageEntryId,
                messageText,
                isNewSession,
                cancellationToken,
                backend,
                initiatorActorId,
                targetActorId);
        }

        try
        {
            return await AwaitRunCompletionAsync(session, run).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return await HandleCallerCancellationAsync(session, run).ConfigureAwait(false);
        }
    }

    public Task CancelAsync(
        ConversationId conversationId,
        CancellationToken cancellationToken = default)
    {
        if (conversationId == default)
        {
            throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        }

        LiveSession? session;
        LiveRun? run;

        lock (_sessionsSync)
        {
            if (!_sessions.TryGetValue(conversationId, out session))
            {
                return Task.CompletedTask;
            }

            run = session.ActiveRun;
            if (run is null || run.StateMachine.IsTerminal)
            {
                return Task.CompletedTask;
            }

            if (run.StateMachine.Status == AgentRunStatus.CancellationRequested)
            {
                return Task.CompletedTask;
            }

            run.StateMachine.TransitionTo(AgentRunStatus.CancellationRequested);
            EmitRunLifecycleLocked(
                session,
                run,
                AgentEventKind.RunCancellationRequested,
                AgentRunStatus.CancellationRequested);
            RevokeRunBrokerLocked(run);
            run.ExecutionCancellation.Cancel();
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public async Task EndAsync(
        ConversationId conversationId,
        CancellationToken cancellationToken = default)
    {
        if (conversationId == default)
        {
            throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        }

        LiveSession? session;
        LiveRun? activeRun;

        lock (_sessionsSync)
        {
            if (!_sessions.TryGetValue(conversationId, out session))
            {
                return;
            }

            if (session.SessionState.Status == AgentSessionStatus.Ended)
            {
                _sessions.Remove(conversationId);
                return;
            }

            activeRun = session.ActiveRun;

            if (session.SessionState.Status != AgentSessionStatus.Ending)
            {
                session.SessionState.TransitionTo(AgentSessionStatus.Ending);
                EmitSessionLifecycleLocked(
                    session,
                    ResolveSessionCorrelationRunId(session, activeRun),
                    AgentEventKind.SessionEnding,
                    AgentSessionStatus.Ending);
            }

            if (activeRun is not null
                && !activeRun.StateMachine.IsTerminal
                && activeRun.StateMachine.Status != AgentRunStatus.CancellationRequested)
            {
                activeRun.StateMachine.TransitionTo(AgentRunStatus.CancellationRequested);
                EmitRunLifecycleLocked(
                    session,
                    activeRun,
                    AgentEventKind.RunCancellationRequested,
                    AgentRunStatus.CancellationRequested);
                RevokeRunBrokerLocked(activeRun);
                activeRun.ExecutionCancellation.Cancel();
            }
        }

        if (activeRun?.ExecutionTask is { } executionTask)
        {
            try
            {
                await executionTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // EndAsync owns teardown even when execution faults.
            }
        }

        lock (_sessionsSync)
        {
            if (!_sessions.TryGetValue(conversationId, out session))
            {
                return;
            }

            FinalizeEndedSessionLocked(session, activeRun?.RunId);
            _sessions.Remove(conversationId);
        }
    }

    public AgentSessionSnapshot? TryGetSessionSnapshot(ConversationId conversationId)
    {
        if (conversationId == default)
        {
            throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        }

        lock (_sessionsSync)
        {
            if (!_sessions.TryGetValue(conversationId, out var session))
            {
                return null;
            }

            return CreateSessionSnapshotLocked(session);
        }
    }

    public AgentRunSnapshot? TryGetActiveRunSnapshot(ConversationId conversationId)
    {
        if (conversationId == default)
        {
            throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        }

        lock (_sessionsSync)
        {
            if (!_sessions.TryGetValue(conversationId, out var session)
                || session.ActiveRun is null)
            {
                return null;
            }

            return CreateRunSnapshotLocked(session, session.ActiveRun);
        }
    }

    public AgentContextSessionPolicyState GetPolicyState(ConversationId conversationId)
    {
        if (conversationId == default)
        {
            throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        }

        lock (_sessionsSync)
        {
            return CreatePolicyStateLocked(conversationId);
        }
    }

    public bool TrySetSessionOverride(
        ConversationId conversationId,
        AgentSessionContextPolicyLevel level)
    {
        if (conversationId == default)
        {
            throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        }

        if (!Enum.IsDefined(level))
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "Policy level is invalid.");
        }

        lock (_sessionsSync)
        {
            var domainLevel = AgentContextPolicyLevelMapper.ToDomain(level);
            _sessionPolicyOverrides[conversationId] = domainLevel;
            return true;
        }
    }

    public bool ClearSessionOverride(ConversationId conversationId)
    {
        if (conversationId == default)
        {
            throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        }

        lock (_sessionsSync)
        {
            return _sessionPolicyOverrides.Remove(conversationId);
        }
    }

    private LiveSession GetOrCreateSessionLocked(
        ConversationId conversationId,
        ActorId targetActorId,
        IAgentBackend backend,
        out bool isNewSession)
    {
        if (_sessions.TryGetValue(conversationId, out var existing))
        {
            isNewSession = false;
            return existing;
        }

        var session = new LiveSession(
            AgentSessionId.New(),
            conversationId,
            targetActorId,
            backend.BackendId,
            backend.BackendVersion,
            backend.CapabilitySnapshot);

        _sessions[conversationId] = session;
        isNewSession = true;
        return session;
    }

    private LiveRun BeginAdmittedRunLocked(
        LiveSession session,
        ConversationEntryId messageEntryId,
        string messageText,
        bool isNewSession,
        CancellationToken cancellationToken,
        IAgentBackend backend,
        ActorId initiatorActorId,
        ActorId targetActorId)
    {
        var run = new LiveRun(ExecutionRunId.New());
        session.ActiveRun = run;
        session.LastAdmittedRunId = run.RunId;

        EmitRunLifecycleLocked(session, run, AgentEventKind.RunCreated, AgentRunStatus.Created);

        if (isNewSession)
        {
            EmitSessionLifecycleLocked(
                session,
                run.RunId,
                AgentEventKind.SessionReady,
                AgentSessionStatus.Ready);
            EmitCapabilitySnapshotLocked(session, run.RunId, session.CapabilitySnapshot);
        }

        run.StateMachine.TransitionTo(AgentRunStatus.Accepted);
        EmitRunLifecycleLocked(session, run, AgentEventKind.RunAccepted, AgentRunStatus.Accepted);

        EmitMessageLocked(
            session,
            run,
            AgentEventKind.UserMessageAdmitted,
            messageEntryId,
            messageText,
            AgentActivityEvidenceLevel.ZaideExecuted);

        session.SessionState.TransitionTo(AgentSessionStatus.Running);
        EmitSessionLifecycleLocked(
            session,
            run.RunId,
            AgentEventKind.SessionRunning,
            AgentSessionStatus.Running);

        var contextManifest = AssembleContextManifestLocked(session, run);

        run.ExecutionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        run.StateMachine.TransitionTo(AgentRunStatus.Running);
        EmitRunLifecycleLocked(session, run, AgentEventKind.RunRunning, AgentRunStatus.Running);
        var request = new AgentBackendRequest(
            session.SessionId,
            run.RunId,
            session.ConversationId,
            initiatorActorId,
            targetActorId,
            messageEntryId,
            messageText,
            contextManifest);
        var executionContext = CreateExecutionContextLocked(
            session,
            run,
            backend,
            request,
            initiatorActorId,
            targetActorId);

        if (contextManifest is not null)
        {
            EmitContextDisclosedLocked(session, run, contextManifest);
        }

        run.ExecutionTask = ObserveBackendAsync(
            session,
            run,
            backend,
            executionContext,
            run.ExecutionCancellation.Token);

        return run;
    }

    private async Task<AgentRunSnapshot> AwaitRunCompletionAsync(LiveSession session, LiveRun run)
    {
        try
        {
            if (run.ExecutionTask is not null)
            {
                await run.ExecutionTask.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (run.ExecutionCancellation.IsCancellationRequested)
        {
            // Terminal cancellation is normalized by the backend observer.
        }
        finally
        {
            run.ExecutionCancellation.Dispose();
        }

        lock (_sessionsSync)
        {
            if (!_sessions.TryGetValue(session.ConversationId, out var currentSession))
            {
                return CreateRunSnapshotLocked(session, run);
            }

            if (currentSession.ActiveRun?.RunId == run.RunId && !run.StateMachine.IsTerminal)
            {
                run.StateMachine.TransitionTo(AgentRunStatus.Cancelled);
                EmitRunLifecycleLocked(
                    currentSession,
                    run,
                    AgentEventKind.RunCancelled,
                    AgentRunStatus.Cancelled);
                ClearActiveRunLocked(currentSession, run);
            }

            return CreateRunSnapshotLocked(currentSession, run);
        }
    }

    private async Task ObserveBackendAsync(
        LiveSession session,
        LiveRun run,
        IAgentBackend backend,
        AgentBackendExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var backendEvent in backend.ExecuteAsync(context, cancellationToken)
                               .ConfigureAwait(false))
            {
                lock (_sessionsSync)
                {
                    if (!_sessions.TryGetValue(session.ConversationId, out var currentSession)
                        || currentSession.ActiveRun?.RunId != run.RunId)
                    {
                        return;
                    }

                    ProcessBackendEventLocked(currentSession, run, backendEvent);
                    if (run.StateMachine.IsTerminal)
                    {
                        ClearActiveRunLocked(currentSession, run);
                        return;
                    }
                }
            }

            lock (_sessionsSync)
            {
                if (_sessions.TryGetValue(session.ConversationId, out var currentSession)
                    && currentSession.ActiveRun?.RunId == run.RunId
                    && !run.StateMachine.IsTerminal)
                {
                    run.StateMachine.TransitionTo(AgentRunStatus.Indeterminate);
                    EmitRunLifecycleLocked(
                        currentSession,
                        run,
                        AgentEventKind.RunIndeterminate,
                        AgentRunStatus.Indeterminate);
                    ClearActiveRunLocked(currentSession, run);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            lock (_sessionsSync)
            {
                if (_sessions.TryGetValue(session.ConversationId, out var currentSession)
                    && currentSession.ActiveRun?.RunId == run.RunId
                    && !run.StateMachine.IsTerminal)
                {
                    run.StateMachine.TransitionTo(AgentRunStatus.Cancelled);
                    EmitRunLifecycleLocked(
                        currentSession,
                        run,
                        AgentEventKind.RunCancelled,
                        AgentRunStatus.Cancelled);
                    ClearActiveRunLocked(currentSession, run);
                }
            }

            throw;
        }
        catch (Exception exception)
        {
            TerminalizeUnexpectedBackendFaultLocked(session, run, exception);
        }
    }

    private void TerminalizeUnexpectedBackendFaultLocked(
        LiveSession session,
        LiveRun run,
        Exception exception)
    {
        lock (_sessionsSync)
        {
            if (!_sessions.TryGetValue(session.ConversationId, out var currentSession)
                || currentSession.ActiveRun?.RunId != run.RunId
                || run.StateMachine.IsTerminal)
            {
                return;
            }

            var reason = exception.Message;
            if (string.IsNullOrWhiteSpace(reason))
            {
                reason = exception.GetType().Name;
            }

            EmitFailureLocked(
                currentSession,
                run,
                AgentFailureKind.Indeterminate,
                reason,
                DateTimeOffset.UtcNow);
            run.StateMachine.TransitionTo(AgentRunStatus.Indeterminate);
            EmitRunLifecycleLocked(
                currentSession,
                run,
                AgentEventKind.RunIndeterminate,
                AgentRunStatus.Indeterminate);
            ClearActiveRunLocked(currentSession, run);
        }
    }

    private void ProcessBackendEventLocked(
        LiveSession session,
        LiveRun run,
        AgentBackendEvent backendEvent)
    {
        switch (backendEvent.Kind)
        {
            case AgentBackendEventKind.MessageCompleted:
                if (backendEvent.Payload is not AgentBackendMessageCompletedPayload messagePayload)
                {
                    throw new InvalidOperationException("Backend message payload is missing.");
                }

                var assistantEntryId = ConversationEntryId.New();
                EmitMessageLocked(
                    session,
                    run,
                    AgentEventKind.AssistantMessageCompleted,
                    assistantEntryId,
                    messagePayload.AssistantText,
                    AgentActivityEvidenceLevel.BackendExecutedAndReported);

                run.StateMachine.TransitionTo(AgentRunStatus.Completed);
                EmitRunLifecycleLocked(
                    session,
                    run,
                    AgentEventKind.RunCompleted,
                    AgentRunStatus.Completed);
                break;

            case AgentBackendEventKind.FailureObserved:
                if (backendEvent.Payload is not AgentBackendFailurePayload failurePayload)
                {
                    throw new InvalidOperationException("Backend failure payload is missing.");
                }

                var terminalStatus = MapFailureKindToTerminalStatus(failurePayload.FailureKind);
                EmitFailureLocked(
                    session,
                    run,
                    failurePayload.FailureKind,
                    failurePayload.Reason,
                    backendEvent.OccurredAtUtc);

                run.StateMachine.TransitionTo(terminalStatus);
                EmitRunLifecycleLocked(
                    session,
                    run,
                    MapTerminalStatusToEventKind(terminalStatus),
                    terminalStatus);
                break;

            default:
                throw new InvalidOperationException($"Unsupported backend event kind '{backendEvent.Kind}'.");
        }
    }

    private void ClearActiveRunLocked(LiveSession session, LiveRun completedRun)
    {
        RevokeRunBrokerLocked(completedRun);
        session.ActiveRun = null;

        if (session.SessionState.Status == AgentSessionStatus.Running)
        {
            session.SessionState.TransitionTo(AgentSessionStatus.Ready);
            EmitSessionLifecycleLocked(
                session,
                completedRun.RunId,
                AgentEventKind.SessionReady,
                AgentSessionStatus.Ready);
        }
    }

    private AgentRunSnapshot RejectRunLocked(LiveSession session, string reason)
    {
        var run = new LiveRun(ExecutionRunId.New());
        EmitRunLifecycleLocked(session, run, AgentEventKind.RunCreated, AgentRunStatus.Created);
        run.StateMachine.TransitionTo(AgentRunStatus.Rejected);
        EmitRunLifecycleLocked(session, run, AgentEventKind.RunRejected, AgentRunStatus.Rejected);
        EmitFailureLocked(
            session,
            run,
            AgentFailureKind.Execution,
            reason,
            DateTimeOffset.UtcNow);
        return CreateRunSnapshotLocked(session, run);
    }

    private AgentRunSnapshot CreateImmediateRejection(
        ConversationId conversationId,
        ActorId targetActorId,
        AgentBackendId backendId,
        string reason)
    {
        lock (_sessionsSync)
        {
            if (_sessions.TryGetValue(conversationId, out var existing))
            {
                return RejectRunLocked(existing, reason);
            }

            var ephemeralSession = new LiveSession(
                AgentSessionId.New(),
                conversationId,
                targetActorId,
                backendId,
                backendVersion: "unknown",
                CreateUnavailableCapabilitySnapshot(backendId));

            return RejectRunLocked(ephemeralSession, reason);
        }
    }

    private Task<AgentRunSnapshot> HandleCallerCancellationAsync(LiveSession session, LiveRun run)
    {
        lock (_sessionsSync)
        {
            if (session.ActiveRun?.RunId == run.RunId
                && !run.StateMachine.IsTerminal
                && run.StateMachine.Status != AgentRunStatus.CancellationRequested)
            {
                run.StateMachine.TransitionTo(AgentRunStatus.CancellationRequested);
                EmitRunLifecycleLocked(
                    session,
                    run,
                    AgentEventKind.RunCancellationRequested,
                    AgentRunStatus.CancellationRequested);
                RevokeRunBrokerLocked(run);
                run.ExecutionCancellation?.Cancel();
            }

            if (session.ActiveRun?.RunId == run.RunId && !run.StateMachine.IsTerminal)
            {
                run.StateMachine.TransitionTo(AgentRunStatus.Cancelled);
                EmitRunLifecycleLocked(
                    session,
                    run,
                    AgentEventKind.RunCancelled,
                    AgentRunStatus.Cancelled);
                ClearActiveRunLocked(session, run);
            }

            return Task.FromResult(CreateRunSnapshotLocked(session, run));
        }
    }

    private void FinalizeEndedSessionLocked(LiveSession session, ExecutionRunId? endingRunId)
    {
        if (session.ActiveRun is { } activeRun && !activeRun.StateMachine.IsTerminal)
        {
            activeRun.StateMachine.TransitionTo(AgentRunStatus.Cancelled);
            EmitRunLifecycleLocked(
                session,
                activeRun,
                AgentEventKind.RunCancelled,
                AgentRunStatus.Cancelled);
            session.ActiveRun = null;
        }

        if (session.SessionState.Status != AgentSessionStatus.Ended)
        {
            session.SessionState.TransitionTo(AgentSessionStatus.Ended);
            EmitSessionLifecycleLocked(
                session,
                endingRunId ?? session.LastAdmittedRunId
                    ?? throw new InvalidOperationException(
                        "Session end requires a previously admitted run correlation id."),
                AgentEventKind.SessionEnded,
                AgentSessionStatus.Ended);
        }
    }

    private static ExecutionRunId ResolveSessionCorrelationRunId(
        LiveSession session,
        LiveRun? activeRun) =>
        activeRun?.RunId
            ?? session.LastAdmittedRunId
            ?? throw new InvalidOperationException(
                "Session lifecycle events require a previously admitted run correlation id.");

    private void EmitSessionLifecycleLocked(
        LiveSession session,
        ExecutionRunId runId,
        AgentEventKind kind,
        AgentSessionStatus status,
        AgentEventId? causationEventId = null)
    {
        var agentEvent = CreateEventLocked(
            session,
            runId,
            kind,
            new AgentSessionLifecyclePayload(status),
            AgentActivityEvidenceLevel.ZaideExecuted,
            occurredAtUtc: DateTimeOffset.UtcNow,
            causationEventId: causationEventId);

        _eventStream.Publish(agentEvent);
    }

    private void EmitRunLifecycleLocked(
        LiveSession session,
        LiveRun run,
        AgentEventKind kind,
        AgentRunStatus status,
        AgentEventId? causationEventId = null)
    {
        var agentEvent = CreateEventLocked(
            session,
            run.RunId,
            kind,
            new AgentRunLifecyclePayload(status),
            AgentActivityEvidenceLevel.ZaideExecuted,
            occurredAtUtc: DateTimeOffset.UtcNow,
            causationEventId: causationEventId);

        run.LastLifecycleEventId = agentEvent.EventId;
        _eventStream.Publish(agentEvent);
    }

    private void EmitMessageLocked(
        LiveSession session,
        LiveRun run,
        AgentEventKind kind,
        ConversationEntryId messageEntryId,
        string text,
        AgentActivityEvidenceLevel evidenceLevel)
    {
        var agentEvent = CreateEventLocked(
            session,
            run.RunId,
            kind,
            new AgentMessagePayload(messageEntryId, text),
            evidenceLevel,
            occurredAtUtc: DateTimeOffset.UtcNow,
            causationEventId: run.LastLifecycleEventId);

        _eventStream.Publish(agentEvent);
    }

    private void EmitFailureLocked(
        LiveSession session,
        LiveRun run,
        AgentFailureKind failureKind,
        string reason,
        DateTimeOffset occurredAtUtc)
    {
        var agentEvent = CreateEventLocked(
            session,
            run.RunId,
            AgentEventKind.FailureReported,
            new AgentFailurePayload(failureKind, reason),
            AgentActivityEvidenceLevel.ZaideMediated,
            occurredAtUtc,
            causationEventId: run.LastLifecycleEventId);

        _eventStream.Publish(agentEvent);
    }

    private void EmitCapabilitySnapshotLocked(
        LiveSession session,
        ExecutionRunId runId,
        AgentCapabilitySnapshot snapshot)
    {
        var agentEvent = CreateEventLocked(
            session,
            runId,
            AgentEventKind.CapabilitySnapshotChanged,
            new AgentCapabilityChangedPayload(snapshot),
            AgentActivityEvidenceLevel.ZaideExecuted,
            occurredAtUtc: DateTimeOffset.UtcNow);

        _eventStream.Publish(agentEvent);
    }

    private void EmitContextDisclosedLocked(
        LiveSession session,
        LiveRun run,
        AgentContextManifest manifest)
    {
        var payload = CreateDisclosurePayloadFromManifest(manifest);
        var agentEvent = CreateEventLocked(
            session,
            run.RunId,
            AgentEventKind.ContextDisclosed,
            payload,
            AgentActivityEvidenceLevel.ZaideExecuted,
            occurredAtUtc: DateTimeOffset.UtcNow);

        _eventStream.Publish(agentEvent);
    }

    private AgentEvent CreateEventLocked(
        LiveSession session,
        ExecutionRunId runId,
        AgentEventKind kind,
        AgentEventPayload payload,
        AgentActivityEvidenceLevel evidenceLevel,
        DateTimeOffset occurredAtUtc,
        AgentEventId? causationEventId = null)
    {
        var receivedAtUtc = DateTimeOffset.UtcNow;
        if (receivedAtUtc < occurredAtUtc)
        {
            receivedAtUtc = occurredAtUtc;
        }

        var agentEvent = new AgentEvent(
            AgentEventId.New(),
            AgentEvent.CurrentSchemaVersion,
            session.SessionId,
            runId,
            session.ConversationId,
            session.BackendId,
            session.NextSequence++,
            occurredAtUtc,
            receivedAtUtc,
            causationEventId,
            evidenceLevel,
            kind,
            payload);

        return agentEvent;
    }

    private static AgentSessionSnapshot CreateSessionSnapshotLocked(LiveSession session) =>
        new(
            session.SessionId,
            session.ConversationId,
            session.AgentIdentity,
            session.BackendId,
            session.BackendVersion,
            session.SessionState.Status,
            session.CapabilitySnapshot,
            session.ActiveRun?.RunId);

    private static AgentRunSnapshot CreateRunSnapshotLocked(LiveSession session, LiveRun run) =>
        new(
            run.RunId,
            session.SessionId,
            session.ConversationId,
            ConversationEntryCorrelationId.FromValue(run.RunId.Value),
            run.StateMachine.Status);

    private static AgentRunStatus MapFailureKindToTerminalStatus(AgentFailureKind failureKind) =>
        failureKind switch
        {
            AgentFailureKind.Timeout => AgentRunStatus.TimedOut,
            AgentFailureKind.Cancellation => AgentRunStatus.Cancelled,
            AgentFailureKind.Transport => AgentRunStatus.Disconnected,
            AgentFailureKind.Indeterminate => AgentRunStatus.Indeterminate,
            _ => AgentRunStatus.Failed,
        };

    private static AgentEventKind MapTerminalStatusToEventKind(AgentRunStatus status) =>
        status switch
        {
            AgentRunStatus.Completed => AgentEventKind.RunCompleted,
            AgentRunStatus.Failed => AgentEventKind.RunFailed,
            AgentRunStatus.Cancelled => AgentEventKind.RunCancelled,
            AgentRunStatus.TimedOut => AgentEventKind.RunTimedOut,
            AgentRunStatus.Disconnected => AgentEventKind.RunDisconnected,
            AgentRunStatus.Indeterminate => AgentEventKind.RunIndeterminate,
            _ => throw new InvalidOperationException($"Unsupported terminal run status '{status}'."),
        };

    private static AgentCapabilitySnapshot CreateUnavailableCapabilitySnapshot(AgentBackendId backendId) =>
        AgentCapabilitySnapshot.CreateInitial(
            backendId,
            new[]
            {
                AgentCapabilityRow.Create(
                    AgentCapabilityId.MessageCompletion,
                    AgentCapabilityState.Create(
                        advertised: AgentCapabilityFactValue.Unavailable,
                        available: AgentCapabilityFactValue.Unavailable,
                        configured: AgentCapabilityFactValue.Unavailable,
                        permitted: AgentCapabilityFactValue.Unknown,
                        degraded: AgentCapabilityFactValue.NotSupported,
                        currentlyUsable: AgentCapabilityFactValue.Unavailable)),
            });

    private sealed class LiveSession
    {
        public LiveSession(
            AgentSessionId sessionId,
            ConversationId conversationId,
            ActorId agentIdentity,
            AgentBackendId backendId,
            string backendVersion,
            AgentCapabilitySnapshot capabilitySnapshot)
        {
            SessionId = sessionId;
            ConversationId = conversationId;
            AgentIdentity = agentIdentity;
            BackendId = backendId;
            BackendVersion = backendVersion;
            CapabilitySnapshot = capabilitySnapshot;
            SessionState = new AgentSessionStateMachine(AgentSessionStatus.Ready);
        }

        public AgentSessionId SessionId { get; }

        public ConversationId ConversationId { get; }

        public ActorId AgentIdentity { get; }

        public AgentBackendId BackendId { get; }

        public string BackendVersion { get; }

        public AgentCapabilitySnapshot CapabilitySnapshot { get; set; }

        public AgentSessionStateMachine SessionState { get; }

        public LiveRun? ActiveRun { get; set; }

        public ExecutionRunId? LastAdmittedRunId { get; set; }

        public long NextSequence { get; set; } = 1;
    }

    private sealed class LiveRun
    {
        public LiveRun(ExecutionRunId runId)
        {
            RunId = runId;
            StateMachine = new AgentRunStateMachine(AgentRunStatus.Created);
        }

        public ExecutionRunId RunId { get; }

        public AgentRunStateMachine StateMachine { get; }

        public AgentEventId? LastLifecycleEventId { get; set; }

        public CancellationTokenSource ExecutionCancellation { get; set; } = null!;

        public Task? ExecutionTask { get; set; }

        public ContractAgentActionBroker? ActionBroker { get; set; }
    }

    private const string ContextAssemblyFailureReason = "IDE context assembly failed.";

    private AgentContextManifest? AssembleContextManifestLocked(
        LiveSession session,
        LiveRun run)
    {
        if (_contextManifestBuilder is null || _contextSnapshotSources is null)
        {
            return null;
        }

        try
        {
            var policy = ResolveContextPolicyLocked(session.ConversationId);
            var assembledAtUtc = DateTimeOffset.UtcNow;
            return _contextManifestBuilder.Build(
                session.SessionId,
                run.RunId,
                session.ConversationId,
                policy,
                _contextSnapshotSources,
                assembledAtUtc);
        }
        catch (Exception)
        {
            EmitFailureLocked(
                session,
                run,
                AgentFailureKind.Indeterminate,
                ContextAssemblyFailureReason,
                DateTimeOffset.UtcNow);
            return null;
        }
    }

    private AgentContextDisclosurePayload CreateDisclosurePayloadFromManifest(
        AgentContextManifest manifest)
    {
        var disclosedSourceIds = manifest.Items
            .Select(item => item.SourceId)
            .Distinct()
            .OrderBy(sourceId => sourceId.Value, StringComparer.Ordinal)
            .ToArray();

        var redactionSummary = CreateRedactionSummary(manifest);
        var boundarySummary = CreateBoundarySummary(manifest);

        return new AgentContextDisclosurePayload(
            manifest.SessionId,
            manifest.RunId,
            manifest.ConversationId,
            manifest.PolicyLevelApplied,
            disclosedSourceIds,
            manifest.Items.Count,
            manifest.TotalEstimatedTokenCount,
            redactionSummary,
            boundarySummary);
    }

    private AgentContextDisclosureRedactionSummary CreateRedactionSummary(
        AgentContextManifest manifest)
    {
        int noRedaction = 0;
        int partialRedaction = 0;
        int fullRedaction = 0;
        int processingFailed = 0;

        foreach (var item in manifest.Items)
        {
            switch (item.RedactionState)
            {
                case AgentContextRedactionState.None:
                    noRedaction++;
                    break;
                case AgentContextRedactionState.Partial:
                    partialRedaction++;
                    break;
                case AgentContextRedactionState.Full:
                    fullRedaction++;
                    break;
                case AgentContextRedactionState.ProcessingFailed:
                    processingFailed++;
                    break;
            }
        }

        return new AgentContextDisclosureRedactionSummary(
            noRedaction,
            partialRedaction,
            fullRedaction,
            processingFailed);
    }

    private AgentContextDisclosureBoundarySummary CreateBoundarySummary(
        AgentContextManifest manifest)
    {
        var excludedSourceIds = new List<AgentContextSourceId>();
        var hardExclusionIds = new List<AgentContextHardExclusionId>();
        var truncatedSourceIds = new List<AgentContextSourceId>();
        int hardExclusionCount = 0;
        int truncatedItemCount = 0;
        int droppedItemCount = 0;

        foreach (var exclusion in manifest.ExclusionDecisions)
        {
            if (exclusion.IsHardExclusion)
            {
                if (exclusion.HardExclusionId is { } hardExclusionId)
                {
                    hardExclusionIds.Add(hardExclusionId);
                }
                hardExclusionCount++;
            }
            else if (exclusion.SourceId is { } sourceId)
            {
                excludedSourceIds.Add(sourceId);
            }
        }

        foreach (var truncation in manifest.TruncationDecisions)
        {
            if (truncation.ItemDropped)
            {
                droppedItemCount++;
            }
            else if (truncation.ItemTruncated)
            {
                truncatedItemCount++;
                truncatedSourceIds.Add(truncation.SourceId);
            }
        }

        return new AgentContextDisclosureBoundarySummary(
            excludedSourceIds.Count,
            hardExclusionCount,
            truncatedItemCount,
            droppedItemCount,
            excludedSourceIds,
            hardExclusionIds,
            truncatedSourceIds);
    }

    private AgentBackendExecutionContext CreateExecutionContextLocked(
        LiveSession session,
        LiveRun run,
        IAgentBackend backend,
        AgentBackendRequest request,
        ActorId initiatorActorId,
        ActorId targetActorId)
    {
        if (backend is not IAgentActionRequestCapableBackend
            || _brokerFactory is null
            || _auditStore is null)
        {
            return new AgentBackendExecutionContext(request, new UnavailableAgentActionBroker());
        }

        var publisher = new RunScopedAgentActionEventPublisher(
            session.SessionId,
            run.RunId,
            session.ConversationId,
            session.BackendId,
            _eventStream,
            _auditStore,
            () => session.NextSequence++,
            _sessionsSync);

        var broker = (ContractAgentActionBroker)_brokerFactory.CreateRunScopedBroker(
            session.SessionId,
            run.RunId,
            session.ConversationId,
            initiatorActorId,
            targetActorId,
            session.BackendId,
            publisher);
        run.ActionBroker = broker;
        return new AgentBackendExecutionContext(request, broker);
    }

    private void RevokeRunBrokerLocked(LiveRun? run)
    {
        run?.ActionBroker?.Revoke();
        if (run is not null)
        {
            run.ActionBroker = null;
        }
    }

    private void OnWorkspaceScopeInvalidated()
    {
        lock (_sessionsSync)
        {
            foreach (var session in _sessions.Values)
            {
                RevokeRunBrokerLocked(session.ActiveRun);
            }
        }
    }

    private AgentContextPolicy ResolveContextPolicyLocked(ConversationId conversationId)
    {
        AgentContextSessionOverride? sessionOverride = null;
        if (_sessionPolicyOverrides.TryGetValue(conversationId, out var overrideLevel))
        {
            sessionOverride = new AgentContextSessionOverride(overrideLevel);
        }

        return new AgentContextPolicy(
            AgentContextApplicationDefault.Level,
            sessionOverride);
    }

    private AgentContextSessionPolicyState CreatePolicyStateLocked(ConversationId conversationId)
    {
        var applicationDefault = AgentContextPolicyLevelMapper.ToContract(
            AgentContextApplicationDefault.Level);
        var policy = ResolveContextPolicyLocked(conversationId);
        var effective = AgentContextPolicyLevelMapper.ToContract(policy.EffectiveLevel);
        var isOverrideActive = policy.SessionOverride?.IsActive == true;
        var statusCaption = isOverrideActive
            ? AgentContextSessionPolicyState.FormatOverrideCaption(effective)
            : AgentContextSessionPolicyState.FormatApplicationDefaultCaption(applicationDefault);

        return new AgentContextSessionPolicyState(
            conversationId,
            applicationDefault,
            effective,
            isOverrideActive,
            statusCaption);
    }
}
