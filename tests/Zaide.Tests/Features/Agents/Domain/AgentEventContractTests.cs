using System;
using Xunit;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents.Domain;

public sealed class AgentEventContractTests
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ReceivedAt = new(2026, 7, 21, 12, 0, 0, 100, TimeSpan.Zero);

    [Fact]
    public void AgentEventId_New_CreatesPrefixedNonDefaultValue()
    {
        var id = AgentEventId.New();

        Assert.NotEqual(default(AgentEventId), id);
        Assert.StartsWith("agent-event:", id.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentEvent_EnforcesMonotonicSequenceAndIdentityBundle()
    {
        var backendId = AgentBackendId.FromValue("backend:legacy-openai-compatible");
        var sessionId = AgentSessionId.New();
        var runId = ExecutionRunId.New();
        var conversationId = ConversationId.NewDirect();

        var agentEvent = CreateLifecycleEvent(
            sequence: 1,
            sessionId,
            runId,
            conversationId,
            backendId,
            AgentEventKind.RunAccepted,
            new AgentRunLifecyclePayload(AgentRunStatus.Accepted));

        Assert.Equal(AgentEvent.CurrentSchemaVersion, agentEvent.SchemaVersion);
        Assert.Equal(1, agentEvent.Sequence);
        Assert.Equal(sessionId, agentEvent.SessionId);
        Assert.Equal(runId, agentEvent.RunId);
        Assert.Equal(conversationId, agentEvent.ConversationId);
        Assert.Equal(backendId, agentEvent.BackendId);
        Assert.Equal(AgentActivityEvidenceLevel.ZaideExecuted, agentEvent.EvidenceLevel);
    }

    [Fact]
    public void AgentEvent_RejectsNonPositiveSequence()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateLifecycleEvent(
                sequence: 0,
                AgentSessionId.New(),
                ExecutionRunId.New(),
                ConversationId.NewDirect(),
                AgentBackendId.FromValue("backend:legacy-openai-compatible"),
                AgentEventKind.RunCreated,
                new AgentRunLifecyclePayload(AgentRunStatus.Created)));

        Assert.Equal("sequence", exception.ParamName);
    }

    [Fact]
    public void AgentEvent_RejectsReceivedBeforeOccurred()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentEvent(
                AgentEventId.New(),
                schemaVersion: AgentEvent.CurrentSchemaVersion,
                sessionId: AgentSessionId.New(),
                runId: ExecutionRunId.New(),
                conversationId: ConversationId.NewDirect(),
                backendId: AgentBackendId.FromValue("backend:legacy-openai-compatible"),
                sequence: 1,
                occurredAtUtc: ReceivedAt,
                receivedAtUtc: OccurredAt,
                causationEventId: null,
                evidenceLevel: AgentActivityEvidenceLevel.ZaideExecuted,
                kind: AgentEventKind.RunCreated,
                payload: new AgentRunLifecyclePayload(AgentRunStatus.Created)));

        Assert.Equal("receivedAtUtc", exception.ParamName);
    }

    [Fact]
    public void AgentEvent_RejectsMismatchedKindAndPayload()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CreateLifecycleEvent(
                sequence: 1,
                AgentSessionId.New(),
                ExecutionRunId.New(),
                ConversationId.NewDirect(),
                AgentBackendId.FromValue("backend:legacy-openai-compatible"),
                AgentEventKind.AssistantMessageCompleted,
                new AgentRunLifecyclePayload(AgentRunStatus.Running)));

        Assert.Equal("payload", exception.ParamName);
    }

    [Fact]
    public void AgentEvent_AcceptsOptionalCausationEventId()
    {
        var causeId = AgentEventId.New();
        var agentEvent = CreateLifecycleEvent(
            sequence: 2,
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            AgentBackendId.FromValue("backend:legacy-openai-compatible"),
            AgentEventKind.RunRunning,
            new AgentRunLifecyclePayload(AgentRunStatus.Running),
            causationEventId: causeId);

        Assert.Equal(causeId, agentEvent.CausationEventId);
    }

    [Fact]
    public void AgentMessagePayload_RejectsBlankText()
    {
        Assert.Throws<ArgumentException>(() =>
            new AgentMessagePayload(
                ConversationEntryId.New(),
                text: "   "));
    }

    [Fact]
    public void AgentEvent_RejectsDefaultOccurredTimestamp()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentEvent(
                AgentEventId.New(),
                schemaVersion: AgentEvent.CurrentSchemaVersion,
                sessionId: AgentSessionId.New(),
                runId: ExecutionRunId.New(),
                conversationId: ConversationId.NewDirect(),
                backendId: AgentBackendId.FromValue("backend:legacy-openai-compatible"),
                sequence: 1,
                occurredAtUtc: default,
                receivedAtUtc: ReceivedAt,
                causationEventId: null,
                evidenceLevel: AgentActivityEvidenceLevel.ZaideExecuted,
                kind: AgentEventKind.RunCreated,
                payload: new AgentRunLifecyclePayload(AgentRunStatus.Created)));

        Assert.Equal("occurredAtUtc", exception.ParamName);
    }

    [Fact]
    public void AgentEvent_RejectsDefaultReceivedTimestamp()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentEvent(
                AgentEventId.New(),
                schemaVersion: AgentEvent.CurrentSchemaVersion,
                sessionId: AgentSessionId.New(),
                runId: ExecutionRunId.New(),
                conversationId: ConversationId.NewDirect(),
                backendId: AgentBackendId.FromValue("backend:legacy-openai-compatible"),
                sequence: 1,
                occurredAtUtc: OccurredAt,
                receivedAtUtc: default,
                causationEventId: null,
                evidenceLevel: AgentActivityEvidenceLevel.ZaideExecuted,
                kind: AgentEventKind.RunCreated,
                payload: new AgentRunLifecyclePayload(AgentRunStatus.Created)));

        Assert.Equal("receivedAtUtc", exception.ParamName);
    }

    [Fact]
    public void AgentEvent_RejectsInvalidEvidenceLevel()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateLifecycleEvent(
                sequence: 1,
                AgentSessionId.New(),
                ExecutionRunId.New(),
                ConversationId.NewDirect(),
                AgentBackendId.FromValue("backend:legacy-openai-compatible"),
                AgentEventKind.RunCreated,
                new AgentRunLifecyclePayload(AgentRunStatus.Created),
                evidenceLevel: (AgentActivityEvidenceLevel)999));

        Assert.Equal("evidenceLevel", exception.ParamName);
    }

    [Fact]
    public void AgentEvent_RejectsCapabilitySnapshotWithMismatchedBackend()
    {
        var envelopeBackend = AgentBackendId.FromValue("backend:legacy-openai-compatible");
        var otherBackend = AgentBackendId.FromValue("backend:other");
        var mismatchedSnapshot = AgentCapabilitySnapshot.CreateInitial(
            otherBackend,
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

        var exception = Assert.Throws<ArgumentException>(() =>
            CreateLifecycleEvent(
                sequence: 1,
                AgentSessionId.New(),
                ExecutionRunId.New(),
                ConversationId.NewDirect(),
                envelopeBackend,
                AgentEventKind.CapabilitySnapshotChanged,
                new AgentCapabilityChangedPayload(mismatchedSnapshot)));

        Assert.Equal("payload", exception.ParamName);
    }

    [Fact]
    public void AgentFailurePayload_RejectsUndefinedFailureKind()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AgentFailurePayload(
                failureKind: (AgentFailureKind)999,
                reason: "failure"));

        Assert.Equal("failureKind", exception.ParamName);
    }

    [Fact]
    public void AgentFailurePayload_RejectsBlankReason()
    {
        Assert.Throws<ArgumentException>(() =>
            new AgentFailurePayload(
                failureKind: AgentFailureKind.Execution,
                reason: "   "));
    }

    private static AgentEvent CreateLifecycleEvent(
        long sequence,
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        AgentBackendId backendId,
        AgentEventKind kind,
        AgentEventPayload payload,
        AgentEventId? causationEventId = null,
        AgentActivityEvidenceLevel evidenceLevel = AgentActivityEvidenceLevel.ZaideExecuted) =>
        new(
            AgentEventId.New(),
            schemaVersion: AgentEvent.CurrentSchemaVersion,
            sessionId: sessionId,
            runId: runId,
            conversationId: conversationId,
            backendId: backendId,
            sequence: sequence,
            occurredAtUtc: OccurredAt,
            receivedAtUtc: ReceivedAt,
            causationEventId: causationEventId,
            evidenceLevel: evidenceLevel,
            kind: kind,
            payload: payload);
}
