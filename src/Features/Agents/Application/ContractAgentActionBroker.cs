using System;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Run-scoped broker that admits and classifies action requests without performing I/O.
/// </summary>
internal sealed class ContractAgentActionBroker : IAgentActionBroker
{
    private readonly AgentSessionId _sessionId;
    private readonly ExecutionRunId _runId;
    private readonly ConversationId _conversationId;
    private readonly ActorId _initiatingActorId;
    private readonly ActorId _targetActorId;
    private readonly AgentBackendId _backendId;
    private readonly WorkspaceIdentity _workspaceIdentity;
    private readonly WorkspaceGeneration _workspaceGeneration;
    private readonly AgentActionRunSlotTracker _runSlot;
    private readonly AgentActionCorrelationRegistry _correlationRegistry;
    private volatile bool _revoked;

    public ContractAgentActionBroker(
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        ActorId initiatingActorId,
        ActorId targetActorId,
        AgentBackendId backendId,
        WorkspaceIdentity workspaceIdentity,
        WorkspaceGeneration workspaceGeneration,
        AgentActionRunSlotTracker runSlot,
        AgentActionCorrelationRegistry correlationRegistry)
    {
        if (sessionId == default)
        {
            throw new ArgumentException("Session id is required.", nameof(sessionId));
        }

        if (runId == default)
        {
            throw new ArgumentException("Run id is required.", nameof(runId));
        }

        if (conversationId == default)
        {
            throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        }

        if (initiatingActorId == default)
        {
            throw new ArgumentException("Initiating actor id is required.", nameof(initiatingActorId));
        }

        if (targetActorId == default)
        {
            throw new ArgumentException("Target actor id is required.", nameof(targetActorId));
        }

        if (backendId == default)
        {
            throw new ArgumentException("Backend id is required.", nameof(backendId));
        }

        if (workspaceIdentity == default)
        {
            throw new ArgumentException("Workspace identity is required.", nameof(workspaceIdentity));
        }

        if (workspaceGeneration == default)
        {
            throw new ArgumentException("Workspace generation is required.", nameof(workspaceGeneration));
        }

        _sessionId = sessionId;
        _runId = runId;
        _conversationId = conversationId;
        _initiatingActorId = initiatingActorId;
        _targetActorId = targetActorId;
        _backendId = backendId;
        _workspaceIdentity = workspaceIdentity;
        _workspaceGeneration = workspaceGeneration;
        _runSlot = runSlot ?? throw new ArgumentNullException(nameof(runSlot));
        _correlationRegistry = correlationRegistry ?? throw new ArgumentNullException(nameof(correlationRegistry));
    }

    public void Revoke() => _revoked = true;

    public ValueTask<AgentActionResult> RequestAsync(
        AgentActionPayload payload,
        string? correlationKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_revoked)
        {
            return ValueTask.FromResult(CreateDeniedResult(
                payload,
                AgentActionFailureKind.BrokerRevoked,
                "Action broker authority was revoked."));
        }

        ArgumentNullException.ThrowIfNull(payload);
        if (!AgentActionPayload.MatchesKind(payload.Kind, payload))
        {
            return ValueTask.FromResult(CreateDeniedResult(
                payload,
                AgentActionFailureKind.InvalidRequest,
                "Action payload kind is inconsistent."));
        }

        AgentActionRequest request;
        try
        {
            request = AgentActionRequestComposer.Compose(
                _sessionId,
                _runId,
                _conversationId,
                _initiatingActorId,
                _targetActorId,
                _backendId,
                _workspaceIdentity,
                _workspaceGeneration,
                payload);
        }
        catch (Exception exception)
        {
            return ValueTask.FromResult(CreateDeniedResult(
                payload,
                AgentActionFailureKind.InvalidRequest,
                exception.Message));
        }

        if (!string.IsNullOrWhiteSpace(correlationKey))
        {
            AgentActionCorrelationKey parsedKey;
            try
            {
                parsedKey = AgentActionCorrelationKey.FromValue(correlationKey);
            }
            catch (Exception exception)
            {
                return ValueTask.FromResult(new AgentActionResult(
                    request.ActionId,
                    request.AttemptId,
                    AgentActionResultKind.Denied,
                    AgentActionFailureKind.InvalidRequest,
                    exception.Message));
            }

            if (_correlationRegistry.TryRejectMismatchedFingerprint(
                    parsedKey,
                    request.Fingerprint,
                    out var mismatch))
            {
                return ValueTask.FromResult(mismatch!);
            }

            if (_correlationRegistry.TryGetTerminalResult(
                    parsedKey,
                    request.Fingerprint,
                    out var replay))
            {
                return ValueTask.FromResult(new AgentActionResult(
                    replay!.ActionId,
                    replay.AttemptId,
                    AgentActionResultKind.DuplicateReplay,
                    null,
                    replay.Summary));
            }
        }

        if (!_runSlot.TryReserve(request.ActionId))
        {
            return ValueTask.FromResult(new AgentActionResult(
                request.ActionId,
                request.AttemptId,
                AgentActionResultKind.Denied,
                AgentActionFailureKind.ConcurrentActionRejected,
                "Only one non-terminal action is allowed per run."));
        }

        var lifecycle = new AgentActionLifecycleState();
        lifecycle.TransitionTo(AgentActionStatus.Classified);

        var classification = AgentActionPolicyClassifier.Classify(request.Payload);
        AgentActionResult terminalResult;
        switch (classification)
        {
            case AgentActionPermissionClassification.DeniedByPolicy:
                lifecycle.TransitionTo(AgentActionStatus.Denied);
                terminalResult = new AgentActionResult(
                    request.ActionId,
                    request.AttemptId,
                    AgentActionResultKind.Denied,
                    AgentActionFailureKind.PolicyDenied,
                    "Action was denied by locked policy.");
                break;

            case AgentActionPermissionClassification.RequiresUserDecision:
                lifecycle.TransitionTo(AgentActionStatus.AwaitingPermissionDecision);
                lifecycle.TransitionTo(AgentActionStatus.PermissionDenied);
                lifecycle.TransitionTo(AgentActionStatus.Denied);
                terminalResult = new AgentActionResult(
                    request.ActionId,
                    request.AttemptId,
                    AgentActionResultKind.Denied,
                    AgentActionFailureKind.PermissionUnavailable,
                    "Permission review is not available in Phase 17 M1.");
                break;

            case AgentActionPermissionClassification.AllowedByLockedPolicy:
                lifecycle.TransitionTo(AgentActionStatus.ReadyToExecute);
                lifecycle.TransitionTo(AgentActionStatus.Failed);
                terminalResult = new AgentActionResult(
                    request.ActionId,
                    request.AttemptId,
                    AgentActionResultKind.Failed,
                    AgentActionFailureKind.ExecutionFailed,
                    "Workspace read execution is not available in Phase 17 M1.");
                break;

            default:
                lifecycle.TransitionTo(AgentActionStatus.Denied);
                terminalResult = new AgentActionResult(
                    request.ActionId,
                    request.AttemptId,
                    AgentActionResultKind.Denied,
                    AgentActionFailureKind.PolicyDenied,
                    "Action was denied by locked policy.");
                break;
        }

        _runSlot.Release(request.ActionId);

        if (!string.IsNullOrWhiteSpace(correlationKey))
        {
            var parsedKey = AgentActionCorrelationKey.FromValue(correlationKey);
            _correlationRegistry.RecordTerminalResult(
                parsedKey,
                request.Fingerprint,
                terminalResult);
        }

        return ValueTask.FromResult(terminalResult);
    }

    private static AgentActionResult CreateDeniedResult(
        AgentActionPayload payload,
        AgentActionFailureKind failureKind,
        string summary)
    {
        _ = payload;
        return new AgentActionResult(
            AgentActionId.New(),
            AgentActionAttemptId.New(),
            AgentActionResultKind.Denied,
            failureKind,
            summary);
    }
}
