using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure.Acp;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents.Acp.Backend;

public sealed class Phase20BackendTests
{
    [Fact]
    public async Task Phase20Backend_CompletesAssistantMessageOnlyAfterPromptTermination()
    {
        var script = new AcpFakeSessionScript
        {
            AgentMessageText = "final answer",
            Updates = new[]
            {
                CreateAgentChunkUpdate("partial"),
            },
        };

        var events = await CollectBackendEventsAsync(script);

        var activityEvents = events
            .Where(e => e.Kind == AgentBackendEventKind.ActivityReported)
            .ToArray();
        Assert.Empty(activityEvents);

        var completion = Assert.Single(
            events, e => e.Kind == AgentBackendEventKind.MessageCompleted);
        var payload = Assert.IsType<AgentBackendMessageCompletedPayload>(completion.Payload);
        Assert.Equal("final answer", payload.AssistantText);
    }

    [Fact]
    public async Task Phase20Backend_EmitsBackendReportedActivityForToolCalls()
    {
        var script = new AcpFakeSessionScript
        {
            Updates = new[]
            {
                new AcpSessionUpdate
                {
                    Kind = AcpSessionUpdateKind.ToolCall,
                    ToolCall = new AcpToolCallWire
                    {
                        ToolCallId = "tc-1",
                        Title = "read_file",
                    },
                },
            },
        };

        var events = await CollectBackendEventsAsync(script);

        var activity = Assert.Single(
            events, e => e.Kind == AgentBackendEventKind.ActivityReported);
        var payload = Assert.IsType<AgentBackendActivityReportedPayload>(activity.Payload);
        Assert.Equal(AcpBackendActivityKind.ToolCall, payload.ActivityKind);
        Assert.Equal("tc-1", payload.AcpCorrelationId);
    }

    [Fact]
    public async Task Phase20Backend_FailsClosedOnAgentIdentityMismatch()
    {
        var callCount = 0;
        var backend = new AcpAgentBackend(
            _ =>
            {
                callCount++;
                var script = callCount == 1
                    ? new AcpFakeSessionScript { AgentName = "agent-a", AgentVersion = "1" }
                    : new AcpFakeSessionScript { AgentName = "agent-b", AgentVersion = "1" };
                return Task.FromResult<IAcpSessionClient>(new AcpFakeSessionClient(script));
            },
            () => "/tmp/zaide-acp");

        var sessionId = AgentSessionId.New();
        await CollectBackendEventsAsync(backend, CreateContext(sessionId));

        var events = await CollectBackendEventsAsync(backend, CreateContext(sessionId));
        var failure = Assert.Single(
            events, e => e.Kind == AgentBackendEventKind.FailureObserved);
        var payload = Assert.IsType<AgentBackendFailurePayload>(failure.Payload);
        Assert.Equal(AgentFailureKind.Transport, payload.FailureKind);
        Assert.Contains("identity mismatch", payload.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Phase20Backend_MapsCancellationStopReasonToFailure()
    {
        var script = new AcpFakeSessionScript
        {
            StopReason = AcpStopReason.Cancelled,
        };

        var events = await CollectBackendEventsAsync(script);
        var failure = Assert.Single(
            events, e => e.Kind == AgentBackendEventKind.FailureObserved);
        var payload = Assert.IsType<AgentBackendFailurePayload>(failure.Payload);
        Assert.Equal(AgentFailureKind.Cancellation, payload.FailureKind);
    }

    [Fact]
    public async Task Phase20Backend_UsesAcpBackendIdentity()
    {
        var backend = CreateBackend(new AcpFakeSessionScript());
        Assert.Equal(AgentBackendIds.Acp, backend.BackendId);
        Assert.Equal(AcpAgentBackend.BackendVersionValue, backend.BackendVersion);
    }

    [Fact]
    public async Task Phase20Backend_SessionService_NormalizesActivityAndCompletion()
    {
        var script = new AcpFakeSessionScript
        {
            AgentMessageText = "done",
            Updates = new[]
            {
                new AcpSessionUpdate
                {
                    Kind = AcpSessionUpdateKind.ToolCall,
                    ToolCall = new AcpToolCallWire
                    {
                        ToolCallId = "tc-2",
                        Title = "plan_step",
                    },
                },
            },
        };

        var backend = CreateBackend(script);
        var stream = new AgentEventStream();
        var sessionService = new AgentSessionService(
            new IAgentBackend[] { backend },
            stream);

        var captured = new List<AgentEvent>();
        stream.Events.Subscribe(captured.Add);

        var conversationId = ConversationId.NewDirect();
        var snapshot = await sessionService.SendAsync(
            conversationId,
            ActorId.FromValue("actor:user"),
            ActorId.FromValue("actor:agent"),
            AgentBackendIds.Acp,
            ConversationEntryId.New(),
            "run through session",
            CancellationToken.None);

        Assert.Equal(AgentRunStatus.Completed, snapshot.Status);
        Assert.Contains(
            captured,
            e => e.Kind == AgentEventKind.BackendActivityReported
                 && e.EvidenceLevel == AgentActivityEvidenceLevel.BackendExecutedAndReported);
        Assert.Contains(captured, e => e.Kind == AgentEventKind.AssistantMessageCompleted);
    }

    private static AcpSessionUpdate CreateAgentChunkUpdate(string text) =>
        new()
        {
            Kind = AcpSessionUpdateKind.AgentMessageChunk,
            ContentChunk = new AcpContentChunk
            {
                Content = AcpContentBlock.FromText(text),
            },
        };

    private static async Task<IReadOnlyList<AgentBackendEvent>> CollectBackendEventsAsync(
        AcpFakeSessionScript script) =>
        await CollectBackendEventsAsync(CreateBackend(script), CreateContext(AgentSessionId.New()));

    private static async Task<IReadOnlyList<AgentBackendEvent>> CollectBackendEventsAsync(
        AcpAgentBackend backend,
        AgentBackendExecutionContext context)
    {
        var events = new List<AgentBackendEvent>();
        await foreach (var backendEvent in backend.ExecuteAsync(context, CancellationToken.None))
        {
            events.Add(backendEvent);
        }

        return events;
    }

    private static AcpAgentBackend CreateBackend(AcpFakeSessionScript script) =>
        new(_ => Task.FromResult<IAcpSessionClient>(new AcpFakeSessionClient(script)), () => "/tmp/zaide-acp");

    private static AgentBackendExecutionContext CreateContext(AgentSessionId sessionId) =>
        new(
            new AgentBackendRequest(
                sessionId,
                ExecutionRunId.New(),
                ConversationId.NewDirect(),
                ActorId.FromValue("actor:user"),
                ActorId.FromValue("actor:agent"),
                ConversationEntryId.New(),
                "hello"),
            new UnavailableAgentActionBroker());
}
