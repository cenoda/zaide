using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Application.Acp;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Agents.Infrastructure.Acp;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Domain;
using Zaide.Tests.Features.Agents;
using Zaide.Tests.Features.Agents.Acp.Backend;

namespace Zaide.Tests.Features.Agents.Acp.Actions;

public sealed class Phase20ActionBridgeTests : IDisposable
{
    private readonly string _workspaceRoot;
    private readonly WorkspaceActionScope _scope;

    public Phase20ActionBridgeTests()
    {
        _workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "zaide-p20-action-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workspaceRoot);
        _scope = FakeWorkspaceActionAuthority.CreateScopeFromDirectory(_workspaceRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_workspaceRoot))
            {
                Directory.Delete(_workspaceRoot, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void Phase20ActionBridge_CapabilityProfile_DoesNotAdvertiseFilesystemBeforeBridge()
    {
        var capabilities = AcpClientCapabilityProfiles.CreateWithoutFilesystemBridge();

        Assert.False(capabilities.Fs.ReadTextFile);
        Assert.False(capabilities.Fs.WriteTextFile);
        Assert.False(capabilities.Terminal);
    }

    [Fact]
    public void Phase20ActionBridge_CapabilityProfile_AdvertisesFilesystemWhenBridgeAvailable()
    {
        var capabilities = AcpClientCapabilityProfiles.CreateWithFilesystemBridge();

        Assert.True(capabilities.Fs.ReadTextFile);
        Assert.True(capabilities.Fs.WriteTextFile);
        Assert.False(capabilities.Terminal);
    }

    [Fact]
    public void Phase20ActionBridge_Backend_ImplementsActionRequestCapableMarkerOnlyWhenBridgeEnabled()
    {
        var actionCapable = new AcpActionCapableAgentBackend(
            new DelegatingAcpSessionClientFactory(
                _ => Task.FromResult<IAcpSessionClient>(new AcpFakeSessionClient(new AcpFakeSessionScript()))),
            () => _workspaceRoot);

        Assert.IsAssignableFrom<IAgentActionRequestCapableBackend>(actionCapable);

        var legacy = new AcpAgentBackend(
            new DelegatingAcpSessionClientFactory(
                _ => Task.FromResult<IAcpSessionClient>(new AcpFakeSessionClient(new AcpFakeSessionScript()))),
            () => _workspaceRoot);

        Assert.IsNotAssignableFrom<IAgentActionRequestCapableBackend>(legacy);
    }

    [Fact]
    public async Task Phase20ActionBridge_Read_MapsAbsolutePathAndRoutesThroughBroker()
    {
        var relativePath = "docs/readme.md";
        Directory.CreateDirectory(Path.Combine(_workspaceRoot, "docs"));
        File.WriteAllText(Path.Combine(_workspaceRoot, relativePath), "hello acp");
        var broker = CreateBroker();
        var absolutePath = Path.Combine(_workspaceRoot, relativePath);
        AcpJsonRpcResponse? response = null;

        var script = new AcpFakeSessionScript
        {
            InboundRequestsDuringPrompt =
            [
                new AcpFakeInboundRequest
                {
                    Request = CreateRequest(
                        AcpMethodNames.FsReadTextFile,
                        new AcpReadTextFileRequestWire
                        {
                            SessionId = "fake-session-1",
                            Path = absolutePath,
                        }),
                    ResponseCallback = captured => response = captured,
                },
            ],
        };

        await ExecuteActionCapableBackendAsync(script, broker);

        Assert.NotNull(response);
        Assert.True(response!.IsSuccess);
        var payload = AcpMessageCodec.DeserializeResult<AcpReadTextFileResponseWire>(response.Result);
        Assert.Equal("hello acp", payload.Content);
    }

    [Fact]
    public async Task Phase20ActionBridge_Write_CreateRoutesThroughBroker()
    {
        var broker = CreateBroker();
        var absolutePath = Path.Combine(_workspaceRoot, "new-file.txt");
        AcpJsonRpcResponse? response = null;

        var script = new AcpFakeSessionScript
        {
            InboundRequestsDuringPrompt =
            [
                new AcpFakeInboundRequest
                {
                    Request = CreateRequest(
                        AcpMethodNames.FsWriteTextFile,
                        new AcpWriteTextFileRequestWire
                        {
                            SessionId = "fake-session-1",
                            Path = absolutePath,
                            Content = "created by acp",
                        }),
                    ResponseCallback = captured => response = captured,
                },
            ],
        };

        await ExecuteActionCapableBackendAsync(script, broker);

        Assert.NotNull(response);
        Assert.True(response!.IsSuccess);
        Assert.Equal("created by acp", File.ReadAllText(absolutePath));
    }

    [Fact]
    public async Task Phase20ActionBridge_Write_ReplaceRoutesThroughBroker()
    {
        var relativePath = "existing.txt";
        var absolutePath = Path.Combine(_workspaceRoot, relativePath);
        File.WriteAllText(absolutePath, "original");
        var broker = CreateBroker();
        AcpJsonRpcResponse? response = null;

        var script = new AcpFakeSessionScript
        {
            InboundRequestsDuringPrompt =
            [
                new AcpFakeInboundRequest
                {
                    Request = CreateRequest(
                        AcpMethodNames.FsWriteTextFile,
                        new AcpWriteTextFileRequestWire
                        {
                            SessionId = "fake-session-1",
                            Path = absolutePath,
                            Content = "replacement",
                        }),
                    ResponseCallback = captured => response = captured,
                },
            ],
        };

        await ExecuteActionCapableBackendAsync(script, broker);

        Assert.NotNull(response);
        Assert.True(response!.IsSuccess);
        Assert.Equal("replacement", File.ReadAllText(absolutePath));
    }

    [Fact]
    public async Task Phase20ActionBridge_PathTraversal_RejectsOutsideWorkspace()
    {
        var broker = CreateBroker();
        AcpJsonRpcResponse? response = null;

        var script = new AcpFakeSessionScript
        {
            InboundRequestsDuringPrompt =
            [
                new AcpFakeInboundRequest
                {
                    Request = CreateRequest(
                        AcpMethodNames.FsReadTextFile,
                        new AcpReadTextFileRequestWire
                        {
                            SessionId = "fake-session-1",
                            Path = "/etc/passwd",
                        }),
                    ResponseCallback = captured => response = captured,
                },
            ],
        };

        await ExecuteActionCapableBackendAsync(script, broker);

        Assert.NotNull(response);
        Assert.False(response!.IsSuccess);
        Assert.Equal(AcpJsonRpcErrorCode.InvalidParams, response.Error!.Code);
    }

    [Fact]
    public async Task Phase20ActionBridge_TerminalMethod_RemainsRejectedByFallbackRouter()
    {
        var capabilities = AcpClientCapabilityProfiles.CreateWithFilesystemBridge();
        var router = new AcpInboundClientRequestRouter(capabilities);
        var response = await router.HandleAsync(
            CreateRequest(AcpMethodNames.TerminalCreate, new { }),
            CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Equal(AcpJsonRpcErrorCode.MethodNotFound, response.Error!.Code);
    }

    [Fact]
    public async Task Phase20ActionBridge_FakeClient_AdvertisesFilesystemOnlyAfterBridgeConfiguration()
    {
        var client = new AcpFakeSessionClient(new AcpFakeSessionScript());
        Assert.False(client.AdvertisedCapabilities.Fs.ReadTextFile);

        client.ConfigureActionBridge(
            null,
            AcpClientCapabilityProfiles.CreateWithFilesystemBridge());

        Assert.True(client.AdvertisedCapabilities.Fs.ReadTextFile);
        Assert.True(client.AdvertisedCapabilities.Fs.WriteTextFile);
    }

    [Fact]
    public async Task Phase20ActionBridge_ToolCallActivity_RemainsBackendReportedNotZaideMediated()
    {
        var script = new AcpFakeSessionScript
        {
            Updates =
            [
                new AcpSessionUpdate
                {
                    Kind = AcpSessionUpdateKind.ToolCall,
                    ToolCall = new AcpToolCallWire
                    {
                        ToolCallId = "direct-tool",
                        Title = "external_tool",
                    },
                },
            ],
        };

        var events = await CollectBackendEventsAsync(script, new UnavailableAgentActionBroker());
        var activity = Assert.Single(events, e => e.Kind == AgentBackendEventKind.ActivityReported);
        var payload = Assert.IsType<AgentBackendActivityReportedPayload>(activity.Payload);
        Assert.Equal(AcpBackendActivityKind.ToolCall, payload.ActivityKind);
    }

    private ContractAgentActionBroker CreateBroker(
        IAgentPermissionReviewService? reviewService = null,
        IAgentFileReader? fileReader = null,
        IAgentFileMutator? fileMutator = null)
    {
        return new ContractAgentActionBroker(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            ActorId.FromValue("actor:user"),
            ActorId.FromValue("actor:agent"),
            AgentBackendIds.Acp,
            new FakeWorkspaceActionAuthority(_scope),
            fileReader ?? new WorkspaceFileReader(),
            fileMutator ?? new WorkspaceFileMutator(),
            new FakeTrustedCommandResolver(),
            new WorkspaceCommandExecutor(),
            new AgentActionRunSlotTracker(),
            new AgentActionCorrelationRegistry(),
            reviewService ?? new AllowingPermissionReviewService());
    }

    private async Task ExecuteActionCapableBackendAsync(
        AcpFakeSessionScript script,
        IAgentActionBroker broker)
    {
        var backend = new AcpActionCapableAgentBackend(
            new DelegatingAcpSessionClientFactory(
                _ => Task.FromResult<IAcpSessionClient>(new AcpFakeSessionClient(script))),
            () => _workspaceRoot);

        await foreach (var _ in backend.ExecuteAsync(CreateContext(broker), CancellationToken.None))
        {
        }
    }

    private async Task<IReadOnlyList<AgentBackendEvent>> CollectBackendEventsAsync(
        AcpFakeSessionScript script,
        IAgentActionBroker broker)
    {
        var backend = new AcpActionCapableAgentBackend(
            new DelegatingAcpSessionClientFactory(
                _ => Task.FromResult<IAcpSessionClient>(new AcpFakeSessionClient(script))),
            () => _workspaceRoot);
        var events = new List<AgentBackendEvent>();
        await foreach (var backendEvent in backend.ExecuteAsync(CreateContext(broker), CancellationToken.None))
        {
            events.Add(backendEvent);
        }

        return events;
    }

    private static AgentBackendExecutionContext CreateContext(IAgentActionBroker broker) =>
        new(
            new AgentBackendRequest(
                AgentSessionId.New(),
                ExecutionRunId.New(),
                ConversationId.NewDirect(),
                ActorId.FromValue("actor:user"),
                ActorId.FromValue("actor:agent"),
                ConversationEntryId.New(),
                "hello"),
            broker);

    private static AcpJsonRpcRequest CreateRequest(string method, object parameters) =>
        new()
        {
            Id = AcpJsonRpcRequestId.FromNumber(1),
            Method = method,
            Params = System.Text.Json.JsonSerializer.SerializeToElement(
                parameters,
                AcpJsonSerializerOptionsFactory.SharedOptions),
        };

    private sealed class AllowingPermissionReviewService : IAgentPermissionReviewService
    {
        public ValueTask<AgentPermissionDecision> RequestDecisionAsync(
            AgentActionRequest request,
            AgentActionDisplaySummary displaySummary,
            WorkspaceActionScope? workspaceScope,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AgentPermissionDecision(
                AgentPermissionDecisionId.New(),
                request.Fingerprint,
                AgentActionPermissionClassification.RequiresUserDecision,
                AgentPermissionDecisionStatus.Published,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                true));
    }
}
