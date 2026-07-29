using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure.Acp;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;
using Zaide.Tests.Features.Agents.Acp.Backend;
using Zaide.Tests.Features.Agents.Acp.Transport;
using Zaide.Tests.Features.Conversations;

namespace Zaide.Tests.Features.Agents.Acp.Integration;

[Collection("AcpProcessIsolation")]
public sealed class Phase20TownhallProjectionTests
{
    [Fact]
    public void BackendActivityReported_ProjectsStructuredSystemNotification()
    {
        var store = ConversationsTestSupport.CreateStore();
        var stream = new AgentEventStream();
        var catalog = ConversationsTestSupport.CreateCatalog();
        using var projection = new AgentConversationEventProjection(stream.Events, store, catalog);

        var conversation = store.CreateDirectConversation(ActorId.HumanUser, ActorId.TownhallAgent);
        var sessionId = AgentSessionId.New();
        var runId = ExecutionRunId.New();

        stream.Publish(
            new AgentEvent(
                AgentEventId.New(),
                AgentEvent.CurrentSchemaVersion,
                sessionId,
                runId,
                conversation.Id,
                AgentBackendIds.Acp,
                sequence: 1,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                causationEventId: null,
                AgentActivityEvidenceLevel.BackendExecutedAndReported,
                AgentEventKind.BackendActivityReported,
                new AgentBackendReportedActivityPayload(
                    AcpBackendActivityKind.ToolCall,
                    "ACP tool call reported by backend: read_file.",
                    "tc-1")));

        var entry = Assert.Single(conversation.Entries);
        Assert.Equal(ConversationEntryKind.SystemNotification, entry.Kind);
        Assert.StartsWith("zaide-backend-activity|v1|", entry.Content, StringComparison.Ordinal);

        var message = TownhallEntryProjection.ToTownhallMessage(entry, catalog);
        Assert.Equal(TownhallMessageKind.AgentAction, message.Kind);
        Assert.Contains("Backend activity: Tool call", message.Content, StringComparison.Ordinal);
        Assert.Contains("[Backend-reported]", message.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AcpToolActivity_ReachesTownhallViaProjectionPath()
    {
        var bindingStore = new AgentActorBackendBindingStore();
        var actorId = ActorId.TownhallAgent;
        var runtime = CreateRuntimeIdentity();

        bindingStore.SetBinding(
            new AgentActorBackendBinding(
                actorId,
                AgentBackendIds.Acp,
                runtime,
                "acp-fake-agent",
                "phase-20-m2"));

        var script = new AcpFakeSessionScript
        {
            AgentName = "acp-fake-agent",
            AgentVersion = "phase-20-m2",
            AgentMessageText = "tool activity complete",
            Updates = new[]
            {
                new AcpSessionUpdate
                {
                    Kind = AcpSessionUpdateKind.ToolCall,
                    ToolCall = new AcpToolCallWire
                    {
                        ToolCallId = "tc-fake-1",
                        Title = "read_file",
                    },
                },
                new AcpSessionUpdate
                {
                    Kind = AcpSessionUpdateKind.ToolCallUpdate,
                    ToolCallUpdate = new AcpToolCallUpdateWire
                    {
                        ToolCallId = "tc-fake-1",
                        Status = "completed",
                    },
                },
            },
        };

        var store = ConversationsTestSupport.CreateStore();
        var catalog = ConversationsTestSupport.CreateCatalog();
        var stream = new AgentEventStream();
        using var projection = new AgentConversationEventProjection(stream.Events, store, catalog);

        var backend = new AcpAgentBackend(
            new DelegatingAcpSessionClientFactory(
                _ => Task.FromResult<IAcpSessionClient>(new AcpFakeSessionClient(script))),
            () => Environment.CurrentDirectory,
            bindingStore);

        var sessionService = new AgentSessionService(new IAgentBackend[] { backend }, stream);
        var conversation = store.CreateDirectConversation(ActorId.HumanUser, actorId);

        var snapshot = await sessionService.SendAsync(
            conversation.Id,
            ActorId.HumanUser,
            actorId,
            AgentBackendIds.Acp,
            ConversationEntryId.New(),
            "show tool activity",
            CancellationToken.None);

        Assert.Equal(AgentRunStatus.Completed, snapshot.Status);

        var activityEntry = conversation.Entries
            .FirstOrDefault(e => e.Content.StartsWith("zaide-backend-activity|v1|", StringComparison.Ordinal));
        Assert.NotNull(activityEntry);

        var message = TownhallEntryProjection.ToTownhallMessage(activityEntry!, catalog);
        Assert.Equal(TownhallMessageKind.AgentAction, message.Kind);
        Assert.Contains("Backend activity:", message.Content, StringComparison.Ordinal);
    }

    private static AcpRuntimeIdentity CreateRuntimeIdentity()
    {
        var options = AcpFakeAgentFixture.CreateLaunchOptions("healthy");
        return new AcpRuntimeIdentity(options.FileName, options.Arguments);
    }
}
