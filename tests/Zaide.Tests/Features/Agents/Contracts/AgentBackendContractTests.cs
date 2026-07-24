using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents.Contracts;

public sealed class AgentBackendContractTests
{
    [Fact]
    public void IAgentBackend_ExposesImmutableIdentityCapabilityAndExecuteAsync()
    {
        var backendType = typeof(IAgentBackend);

        Assert.True(backendType.IsInterface);
        Assert.True(backendType.GetProperty(nameof(IAgentBackend.BackendId))!.CanRead);
        Assert.True(backendType.GetProperty(nameof(IAgentBackend.BackendVersion))!.CanRead);
        Assert.True(backendType.GetProperty(nameof(IAgentBackend.CapabilitySnapshot))!.CanRead);

        var execute = backendType.GetMethod(nameof(IAgentBackend.ExecuteAsync));
        Assert.NotNull(execute);
        Assert.True(typeof(IAsyncEnumerable<AgentBackendEvent>).IsAssignableFrom(execute!.ReturnType));

        var parameters = execute.GetParameters();
        Assert.Equal(typeof(AgentBackendExecutionContext), parameters[0].ParameterType);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
    }

    [Fact]
    public void IAgentActionBroker_ExposesRunScopedRequestAsync()
    {
        var brokerType = typeof(IAgentActionBroker);
        Assert.True(brokerType.IsInterface);

        var requestAsync = brokerType.GetMethod(nameof(IAgentActionBroker.RequestAsync));
        Assert.NotNull(requestAsync);
        Assert.Equal(typeof(ValueTask<AgentActionResult>), requestAsync!.ReturnType);

        var parameters = requestAsync.GetParameters();
        Assert.Equal(typeof(AgentActionPayload), parameters[0].ParameterType);
        Assert.Equal(typeof(string), parameters[1].ParameterType);
        Assert.Equal(typeof(CancellationToken), parameters[2].ParameterType);
    }

    [Fact]
    public void AgentBackendExecutionContext_CarriesRequestAndBroker()
    {
        var request = new AgentBackendRequest(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            ActorId.HumanUser,
            ActorId.PanelSeed("alpha"),
            ConversationEntryId.New(),
            "hello");
        var broker = new UnavailableAgentActionBroker();
        var context = new AgentBackendExecutionContext(request, broker);

        Assert.Equal(request, context.Request);
        Assert.Same(broker, context.Actions);
    }

    [Fact]
    public void IAgentSessionService_ExposesEventFeedSendCancelEndAndSnapshots()
    {
        var serviceType = typeof(IAgentSessionService);

        Assert.True(serviceType.IsInterface);
        Assert.Equal(typeof(IObservable<AgentEvent>), serviceType.GetProperty(nameof(IAgentSessionService.Events))!.PropertyType);

        var send = serviceType.GetMethod(nameof(IAgentSessionService.SendAsync));
        Assert.NotNull(send);
        Assert.Equal(typeof(Task<AgentRunSnapshot>), send!.ReturnType);

        var sendParameters = send.GetParameters().Select(p => p.ParameterType).ToArray();
        Assert.Contains(typeof(ConversationId), sendParameters);
        Assert.Contains(typeof(ActorId), sendParameters);
        Assert.Contains(typeof(AgentBackendId), sendParameters);
        Assert.Contains(typeof(ConversationEntryId), sendParameters);
        Assert.Equal(typeof(string), sendParameters[^2]);
        Assert.Equal(typeof(CancellationToken), sendParameters[^1]);

        Assert.NotNull(serviceType.GetMethod(
            nameof(IAgentSessionService.CancelAsync),
            new[] { typeof(ConversationId), typeof(CancellationToken) }));

        Assert.NotNull(serviceType.GetMethod(
            nameof(IAgentSessionService.EndAsync),
            new[] { typeof(ConversationId), typeof(CancellationToken) }));

        Assert.Equal(
            typeof(AgentSessionSnapshot),
            serviceType.GetMethod(nameof(IAgentSessionService.TryGetSessionSnapshot))!.ReturnType);
        Assert.Equal(
            typeof(AgentRunSnapshot),
            serviceType.GetMethod(nameof(IAgentSessionService.TryGetActiveRunSnapshot))!.ReturnType);
    }

    [Fact]
    public void AgentBackendRequest_CarriesSessionRunConversationAndMessageIdentity()
    {
        var sessionId = AgentSessionId.New();
        var runId = ExecutionRunId.New();
        var conversationId = ConversationId.NewDirect();
        var messageEntryId = ConversationEntryId.New();

        var request = new AgentBackendRequest(
            sessionId,
            runId,
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("alpha"),
            messageEntryId,
            "hello");

        Assert.Equal(sessionId, request.SessionId);
        Assert.Equal(runId, request.RunId);
        Assert.Equal(conversationId, request.ConversationId);
        Assert.Equal(messageEntryId, request.MessageEntryId);
        Assert.Equal("hello", request.MessageText);
    }

    [Fact]
    public void AgentBackendEvent_RequiresMatchingKindAndPayload()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentBackendEvent(
                AgentBackendEventKind.MessageCompleted,
                DateTimeOffset.UtcNow,
                new AgentBackendFailurePayload(AgentFailureKind.Timeout, "timeout")));

        Assert.Equal("payload", exception.ParamName);
    }

    [Fact]
    public void AgentBackendFailurePayload_CarriesTypedFailureKind()
    {
        var payload = new AgentBackendFailurePayload(
            AgentFailureKind.Timeout,
            "request timed out after 120 seconds");

        Assert.Equal(AgentFailureKind.Timeout, payload.FailureKind);
        Assert.Equal("request timed out after 120 seconds", payload.Reason);
    }
}
