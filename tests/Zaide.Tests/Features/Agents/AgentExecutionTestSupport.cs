using System;
using System.Threading.Tasks;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents;

/// <summary>
/// Shared helpers for structured execution coordinator/router tests.
/// </summary>
internal static class AgentExecutionTestSupport
{
    public static AgentExecutionCoordinator CreateCoordinator(
        AgentPanelHost host,
        IAgentExecutionService executionService,
        IConversationStore? conversationStore = null,
        IConversationDraftState? draftState = null)
    {
        if (executionService is not AgentExecutionService concrete)
        {
            throw new ArgumentException(
                "Legacy coordinator wiring requires the concrete AgentExecutionService instance.",
                nameof(executionService));
        }

        var backend = new LegacyOpenAiCompatibleAgentBackend(concrete);
        var session = new AgentSessionService(new[] { backend }, new AgentEventStream());
        var store = conversationStore ?? Conversations.ConversationsTestSupport.CreateStore();
        _ = new AgentConversationEventProjection(session.Events, store, Conversations.ConversationsTestSupport.CreateCatalog());
        return new AgentExecutionCoordinator(
            host,
            session,
            store,
            draftState);
    }

    public static AgentExecutionCoordinator CreateCoordinatorFromHandler(
        AgentPanelHost host,
        Func<string, Task<AgentExecutionResult>> handler,
        IConversationStore? conversationStore = null,
        IConversationDraftState? draftState = null)
    {
        var backend = new ResultMappingAgentBackend(handler);
        var session = new AgentSessionService(new[] { backend }, new AgentEventStream());
        var store = conversationStore ?? Conversations.ConversationsTestSupport.CreateStore();
        _ = new AgentConversationEventProjection(session.Events, store, Conversations.ConversationsTestSupport.CreateCatalog());
        return new AgentExecutionCoordinator(
            host,
            session,
            store,
            draftState);
    }

    public static (AgentExecutionCoordinator Coordinator, FakeAgentBackend Backend, IAgentSessionService Session)
        CreateCoordinatorWithFakeBackend(
            AgentPanelHost host,
            IConversationStore? conversationStore = null,
            IConversationDraftState? draftState = null,
            AgentBackendId? backendId = null)
    {
        var backend = new FakeAgentBackend(
            backendId ?? AgentBackendId.FromValue(LegacyOpenAiCompatibleAgentBackend.BackendIdValue));
        var session = new AgentSessionService(new[] { backend }, new AgentEventStream());
        var store = conversationStore ?? Conversations.ConversationsTestSupport.CreateStore();
        _ = new AgentConversationEventProjection(session.Events, store, Conversations.ConversationsTestSupport.CreateCatalog());
        var coordinator = new AgentExecutionCoordinator(
            host,
            session,
            store,
            draftState);
        return (coordinator, backend, session);
    }

    public static AgentExecutionCoordinatorResult SuccessResult(
        AgentPanelState panel,
        string assistantResponse = "Hello back")
    {
        var run = new ExecutionRun(
            ExecutionRunId.New(),
            panel.ConversationId,
            ActorId.HumanUser,
            panel.ActorId,
            panel.PanelId,
            ExecutionRunOutcome.Success);

        return AgentExecutionCoordinatorResult.Success(run, assistantResponse);
    }

    public static AgentExecutionCoordinatorResult ErrorResult(
        AgentPanelState panel,
        string errorMessage = "Request failed",
        ExecutionRunOutcome outcome = ExecutionRunOutcome.ExecutionFailure)
    {
        var run = new ExecutionRun(
            ExecutionRunId.New(),
            panel.ConversationId,
            ActorId.HumanUser,
            panel.ActorId,
            panel.PanelId,
            outcome);

        return AgentExecutionCoordinatorResult.Failure(run, errorMessage);
    }

    public static AgentExecutionCoordinatorResult RoutingFailureResult(
        AgentPanelState panel,
        string failureReason = "Unknown target")
    {
        var run = new ExecutionRun(
            ExecutionRunId.New(),
            panel.ConversationId,
            ActorId.HumanUser,
            panel.ActorId,
            panel.PanelId,
            ExecutionRunOutcome.RoutingFailure);

        return AgentExecutionCoordinatorResult.RoutingFailure(run, failureReason);
    }

    public static AgentExecutionCoordinatorResult RejectedResult(
        AgentPanelState panel,
        string rejectionReason = "An active run is already in progress for this conversation.")
    {
        var run = new ExecutionRun(
            ExecutionRunId.New(),
            panel.ConversationId,
            ActorId.HumanUser,
            panel.ActorId,
            panel.PanelId,
            ExecutionRunOutcome.Rejected);

        return AgentExecutionCoordinatorResult.Rejected(run, rejectionReason);
    }
}
