using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Infrastructure;
using Zaide.Tests.Features.Conversations;
using Zaide.Tests.Features.Settings.Infrastructure;

namespace Zaide.Tests.Features.Agents;

public sealed class Phase19ToolLoopTests : IDisposable
{
    private readonly string _tempDir;

    public Phase19ToolLoopTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Phase19ToolLoop_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    [Fact]
    public async Task Phase19ToolLoop_CompletesWithAssistantText_WhenProviderReturnsFinalMessage()
    {
        var transport = new ScriptedNativeHarnessProviderTransport();
        transport.Enqueue(NativeHarnessProviderResponse.Success("final answer"));

        var backend = CreateBackend(transport, out var executionService);
        var context = CreateContext(new RecordingAgentActionBroker(), "hello");

        var events = await CollectEventsAsync(backend, context);

        Assert.Single(events);
        Assert.Equal(AgentBackendEventKind.MessageCompleted, events[0].Kind);
        Assert.Equal("final answer", Assert.IsType<AgentBackendMessageCompletedPayload>(events[0].Payload).AssistantText);
        Assert.Single(transport.Requests);
    }

    [Fact]
    public async Task Phase19ToolLoop_ExecutesToolRoundThenCompletes()
    {
        var transport = new ScriptedNativeHarnessProviderTransport();
        transport.Enqueue(NativeHarnessProviderResponse.Success(
            assistantContent: null,
            toolCalls: new[]
            {
                new NativeHarnessProviderToolCall(
                    NativeHarnessToolCallId.FromValue("call-1"),
                    NativeHarnessProviderProtocol.ReadFileToolName,
                    """{"path":"README.md"}"""),
            }));
        transport.Enqueue(NativeHarnessProviderResponse.Success("done after tool"));

        var broker = new RecordingAgentActionBroker();
        broker.SetResult(
            AgentActionKind.ReadFile,
            new AgentActionResult(
                AgentActionId.New(),
                AgentActionAttemptId.New(),
                AgentActionResultKind.Succeeded,
                failureKind: null,
                summary: "read ok",
                content: "file text",
                revision: AgentContentRevision.FromUtf8Text("file text"),
                byteLength: 9));

        var backend = CreateBackend(transport, out _);
        var context = CreateContext(broker, "read the readme");

        var events = await CollectEventsAsync(backend, context);

        Assert.Equal(AgentBackendEventKind.MessageCompleted, events.Single().Kind);
        Assert.Equal("done after tool", Assert.IsType<AgentBackendMessageCompletedPayload>(events[0].Payload).AssistantText);
        Assert.Equal(2, transport.Requests.Count);
        Assert.Single(broker.Payloads);
        Assert.IsType<AgentReadFileActionPayload>(broker.Payloads[0]);
    }

    [Fact]
    public async Task Phase19ToolLoop_ExceedsTurnBudget_AfterConfiguredMaxTurns()
    {
        var transport = new ScriptedNativeHarnessProviderTransport();
        for (var turn = 0; turn < NativeHarnessProviderProtocol.DefaultMaxTurns; turn++)
        {
            transport.Enqueue(NativeHarnessProviderResponse.Success(
                assistantContent: null,
                toolCalls: new[]
                {
                    new NativeHarnessProviderToolCall(
                        NativeHarnessToolCallId.FromValue($"call-{turn}"),
                        NativeHarnessProviderProtocol.ReadFileToolName,
                        """{"path":"README.md"}"""),
                }));
        }

        var broker = new RecordingAgentActionBroker();
        broker.SetResult(
            AgentActionKind.ReadFile,
            SuccessResult("read ok"));

        var backend = CreateBackend(transport, out _);
        var events = await CollectEventsAsync(
            backend,
            CreateContext(broker, "keep calling tools"));

        Assert.Equal(AgentBackendEventKind.FailureObserved, events.Single().Kind);
        var failure = Assert.IsType<AgentBackendFailurePayload>(events[0].Payload);
        Assert.Equal(AgentFailureKind.Execution, failure.FailureKind);
        Assert.Contains("turn budget", failure.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Phase19ToolLoop_Cancellation_ReturnsCancelledFailure()
    {
        var transport = new ScriptedNativeHarnessProviderTransport();
        transport.Enqueue(NativeHarnessProviderResponse.Success("should not finish"));

        var backend = CreateBackend(transport, out _);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var events = await CollectEventsAsync(
            backend,
            CreateContext(new RecordingAgentActionBroker(), "cancel me"),
            cts.Token);

        Assert.Equal(AgentBackendEventKind.FailureObserved, events.Single().Kind);
        var failure = Assert.IsType<AgentBackendFailurePayload>(events[0].Payload);
        Assert.Equal(AgentFailureKind.Cancellation, failure.FailureKind);
    }

    [Fact]
    public async Task Phase19ToolLoop_ProviderFailure_ReturnsFailureObserved()
    {
        var transport = new ScriptedNativeHarnessProviderTransport();
        transport.Enqueue(NativeHarnessProviderResponse.Failure(
            "Provider transport failed.",
            AgentFailureKind.Transport));

        var backend = CreateBackend(transport, out _);
        var events = await CollectEventsAsync(
            backend,
            CreateContext(new RecordingAgentActionBroker(), "hello"));

        Assert.Equal(AgentBackendEventKind.FailureObserved, events.Single().Kind);
        var failure = Assert.IsType<AgentBackendFailurePayload>(events[0].Payload);
        Assert.Equal(AgentFailureKind.Execution, failure.FailureKind);
    }

    [Fact]
    public async Task Phase19ToolLoop_SseReader_ParsesStreamingToolCall()
    {
        var sse = string.Join(
            '\n',
            "data: {\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"id\":\"call-abc\",\"function\":{\"name\":\"read_file\",\"arguments\":\"{\\\"path\\\":\"}}]}}]}",
            "",
            "data: {\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"function\":{\"arguments\":\"\\\"README.md\\\"}\"}}]}}]}",
            "",
            "data: [DONE]",
            "") + '\n';

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sse));
        var response = await NativeHarnessSseReader.ReadCompletionAsync(stream, CancellationToken.None);

        Assert.False(response.IsFailure);
        Assert.Single(response.ToolCalls);
        Assert.Equal("read_file", response.ToolCalls[0].ModelToolName);
        Assert.Equal("""{"path":"README.md"}""", response.ToolCalls[0].ArgumentsJson);
    }

    [Fact]
    public async Task Phase19ToolLoop_InvalidToolArguments_ProduceValidationToolResultAndContinue()
    {
        var transport = new ScriptedNativeHarnessProviderTransport();
        transport.Enqueue(NativeHarnessProviderResponse.Success(
            assistantContent: null,
            toolCalls: new[]
            {
                new NativeHarnessProviderToolCall(
                    NativeHarnessToolCallId.FromValue("call-bad"),
                    NativeHarnessProviderProtocol.ReadFileToolName,
                    """{"not_path":"README.md"}"""),
            }));
        transport.Enqueue(NativeHarnessProviderResponse.Success("recovered"));

        var broker = new RecordingAgentActionBroker();
        var backend = CreateBackend(transport, out _);
        var events = await CollectEventsAsync(
            backend,
            CreateContext(broker, "bad tool args"));

        Assert.Equal("recovered", Assert.IsType<AgentBackendMessageCompletedPayload>(events.Single().Payload).AssistantText);
        Assert.Empty(broker.Payloads);
        var secondRequest = transport.Requests[1];
        var toolMessage = secondRequest.Messages.Last();
        Assert.Equal("tool", toolMessage.Role);
        Assert.Contains("validation failed", toolMessage.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Phase19ToolLoop_UnconfiguredProvider_ReturnsFailure()
    {
        var transport = new ScriptedNativeHarnessProviderTransport();
        var executionService = CreateExecutionService(new AgentExecutionOptions
        {
            BaseUrl = string.Empty,
            ApiKey = string.Empty,
            Model = string.Empty,
        });

        var backend = new NativeHarnessAgentBackend(
            executionService,
            transport,
            new NativeHarnessPriorConversationReader(ConversationsTestSupport.CreateStore()));

        var events = await CollectEventsAsync(
            backend,
            CreateContext(new RecordingAgentActionBroker(), "hello"));

        Assert.Equal(AgentBackendEventKind.FailureObserved, events.Single().Kind);
        var failure = Assert.IsType<AgentBackendFailurePayload>(events[0].Payload);
        Assert.Contains("configuration", failure.Reason, StringComparison.OrdinalIgnoreCase);
    }

    private NativeHarnessAgentBackend CreateBackend(
        ScriptedNativeHarnessProviderTransport transport,
        out AgentExecutionService executionService)
    {
        executionService = CreateExecutionService();
        return new NativeHarnessAgentBackend(
            executionService,
            transport,
            new NativeHarnessPriorConversationReader(ConversationsTestSupport.CreateStore()));
    }

    private AgentExecutionService CreateExecutionService(AgentExecutionOptions? options = null) =>
        Phase19HarnessTestFactory.CreateExecutionService(_tempDir, options);

    private static AgentBackendExecutionContext CreateContext(
        IAgentActionBroker broker,
        string messageText) =>
        new(
            new AgentBackendRequest(
                AgentSessionId.New(),
                ExecutionRunId.New(),
                ConversationId.NewDirect(),
                ActorId.FromValue("actor:user"),
                ActorId.FromValue("actor:agent"),
                ConversationEntryId.FromValue("entry:current"),
                messageText),
            broker);

    private static async Task<IReadOnlyList<AgentBackendEvent>> CollectEventsAsync(
        NativeHarnessAgentBackend backend,
        AgentBackendExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var events = new List<AgentBackendEvent>();
        await foreach (var backendEvent in backend.ExecuteAsync(context, cancellationToken))
        {
            events.Add(backendEvent);
        }

        return events;
    }

    private static AgentActionResult SuccessResult(string summary) =>
        new(
            AgentActionId.New(),
            AgentActionAttemptId.New(),
            AgentActionResultKind.Succeeded,
            failureKind: null,
            summary: summary);
}
