using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
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

public sealed class Phase19BrokerDispatchTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<IDisposable> _disposables = new();

    public Phase19BrokerDispatchTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Phase19Broker_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            try { disposable.Dispose(); } catch { /* best-effort */ }
        }

        _disposables.Clear();

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
    public async Task Phase19BrokerDispatch_ReadFile_DispatchesThroughBroker()
    {
        await AssertDispatchedKindAsync(
            NativeHarnessProviderProtocol.ReadFileToolName,
            """{"path":"src/Program.cs"}""",
            AgentActionKind.ReadFile,
            payload => Assert.IsType<AgentReadFileActionPayload>(payload));
    }

    [Fact]
    public async Task Phase19BrokerDispatch_CreateFile_DispatchesThroughBroker()
    {
        await AssertDispatchedKindAsync(
            NativeHarnessProviderProtocol.CreateFileToolName,
            """{"path":"new.txt","content":"hello"}""",
            AgentActionKind.CreateFile,
            payload => Assert.IsType<AgentCreateFileActionPayload>(payload));
    }

    [Fact]
    public async Task Phase19BrokerDispatch_ReplaceFile_DispatchesThroughBroker()
    {
        var revision = AgentContentRevision.FromUtf8Text("base");
        await AssertDispatchedKindAsync(
            NativeHarnessProviderProtocol.ReplaceFileToolName,
            $$"""{"path":"src/Program.cs","base_revision":"{{revision.Value}}","content":"next"}""",
            AgentActionKind.ReplaceFile,
            payload => Assert.IsType<AgentReplaceFileActionPayload>(payload));
    }

    [Fact]
    public async Task Phase19BrokerDispatch_DeleteFile_DispatchesThroughBroker()
    {
        var revision = AgentContentRevision.FromUtf8Text("base");
        await AssertDispatchedKindAsync(
            NativeHarnessProviderProtocol.DeleteFileToolName,
            $$"""{"path":"old.txt","base_revision":"{{revision.Value}}"}""",
            AgentActionKind.DeleteFile,
            payload => Assert.IsType<AgentDeleteFileActionPayload>(payload));
    }

    [Fact]
    public async Task Phase19BrokerDispatch_ExecuteCommand_DispatchesThroughBroker()
    {
        await AssertDispatchedKindAsync(
            NativeHarnessProviderProtocol.ExecuteCommandToolName,
            """{"executable":"dotnet","arguments":["build"],"working_directory":"."}""",
            AgentActionKind.ExecuteCommand,
            payload => Assert.IsType<AgentExecuteCommandActionPayload>(payload));
    }

    [Fact]
    public async Task Phase19BrokerDispatch_RevokedBroker_ReturnsDeniedToolResultAndContinues()
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
        transport.Enqueue(NativeHarnessProviderResponse.Success("after revoke"));

        var broker = new RecordingAgentActionBroker();
        broker.Revoke();

        var backend = CreateBackend(transport);
        var events = await CollectEventsAsync(backend, CreateContext(broker, "revoked broker"));

        Assert.Equal("after revoke", Assert.IsType<AgentBackendMessageCompletedPayload>(events.Single().Payload).AssistantText);
        Assert.Single(broker.Payloads);
        var toolMessage = transport.Requests[1].Messages.Last();
        Assert.Contains("BrokerRevoked", toolMessage.Content, StringComparison.Ordinal);
    }

    private async Task AssertDispatchedKindAsync(
        string toolName,
        string argumentsJson,
        AgentActionKind expectedKind,
        Action<AgentActionPayload> assertPayload)
    {
        var transport = new ScriptedNativeHarnessProviderTransport();
        transport.Enqueue(NativeHarnessProviderResponse.Success(
            assistantContent: null,
            toolCalls: new[]
            {
                new NativeHarnessProviderToolCall(
                    NativeHarnessToolCallId.FromValue("call-1"),
                    toolName,
                    argumentsJson),
            }));
        transport.Enqueue(NativeHarnessProviderResponse.Success("done"));

        var broker = new RecordingAgentActionBroker();
        broker.SetResult(expectedKind, SuccessResult($"{expectedKind} ok"));

        var backend = CreateBackend(transport);
        await CollectEventsAsync(backend, CreateContext(broker, $"dispatch {expectedKind}"));

        Assert.Single(broker.Payloads);
        Assert.Equal(expectedKind, broker.Payloads[0].Kind);
        assertPayload(broker.Payloads[0]);
    }

    private NativeHarnessAgentBackend CreateBackend(ScriptedNativeHarnessProviderTransport transport)
    {
        var executionService = CreateExecutionService();
        return new NativeHarnessAgentBackend(
            executionService,
            transport,
            new NativeHarnessPriorConversationReader(ConversationsTestSupport.CreateStore()));
    }

    private AgentExecutionService CreateExecutionService() =>
        Phase19HarnessTestFactory.CreateExecutionService(_tempDir, disposableTracker: _disposables);

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
        AgentBackendExecutionContext context)
    {
        var events = new List<AgentBackendEvent>();
        await foreach (var backendEvent in backend.ExecuteAsync(context, CancellationToken.None))
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
