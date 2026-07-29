using System;
using System.IO;
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

public sealed class Phase20PermissionTests : IDisposable
{
    private readonly string _workspaceRoot;
    private readonly WorkspaceActionScope _scope;

    public Phase20PermissionTests()
    {
        _workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "zaide-p20-perm-" + Guid.NewGuid().ToString("N"));
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
    public async Task Phase20Permission_AcpChoice_DoesNotConsumeBrokerDecision()
    {
        var review = new CapturingAllowingPermissionReviewService();
        var broker = CreateBroker(review);
        var bridge = new AcpClientPermissionBridge(
            "fake-session-1",
            new SelectingPermissionChoiceSource("allow_once"));

        var response = await bridge.HandleAsync(
            CreatePermissionRequest("allow_once"),
            CancellationToken.None);

        Assert.True(response.IsSuccess);
        var wire = AcpMessageCodec.DeserializeResult<AcpRequestPermissionResponseWire>(response.Result);
        Assert.Equal("selected", wire.Outcome.Outcome);
        Assert.Null(review.Decision);
    }

    [Fact]
    public async Task Phase20Permission_AcpChoice_IsSeparateFromBrokerAuthorization()
    {
        var review = new CapturingAllowingPermissionReviewService();
        var broker = CreateBroker(review);
        AcpJsonRpcResponse? permissionResponse = null;

        var script = new AcpFakeSessionScript
        {
            InboundRequestsDuringPrompt =
            [
                new AcpFakeInboundRequest
                {
                    Request = CreatePermissionRequest("reject_once"),
                    ResponseCallback = captured => permissionResponse = captured,
                },
            ],
        };

        var backend = new AcpActionCapableAgentBackend(
            new DelegatingAcpSessionClientFactory(
                _ => Task.FromResult<IAcpSessionClient>(new AcpFakeSessionClient(script))),
            () => _workspaceRoot);

        await foreach (var _ in backend.ExecuteAsync(CreateContext(broker), CancellationToken.None))
        {
        }

        Assert.NotNull(permissionResponse);
        Assert.True(permissionResponse!.IsSuccess);
        Assert.Null(review.Decision);
    }

    [Fact]
    public async Task Phase20Permission_StaleBaseThroughBridge_DoesNotConsumePublishedDecision()
    {
        var reader = new CountingAgentFileReader();
        reader.EnqueueReads(
            ConfirmedAbsentTarget,
            ConfirmedAbsentTarget,
            AgentFileReadResult.Success(
                "appeared",
                AgentContentRevision.FromUtf8Text("appeared"),
                byteLength: 8));
        var review = new CapturingAllowingPermissionReviewService();
        var broker = CreateBroker(review, reader);
        var absolutePath = Path.Combine(_workspaceRoot, "stale-create.txt");
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
                            Content = "content",
                        }),
                    ResponseCallback = captured => response = captured,
                },
            ],
        };

        var backend = new AcpActionCapableAgentBackend(
            new DelegatingAcpSessionClientFactory(
                _ => Task.FromResult<IAcpSessionClient>(new AcpFakeSessionClient(script))),
            () => _workspaceRoot);

        await foreach (var _ in backend.ExecuteAsync(CreateContext(broker), CancellationToken.None))
        {
        }

        Assert.NotNull(response);
        Assert.False(response!.IsSuccess);
        Assert.NotNull(review.Decision);
        Assert.Equal(AgentPermissionDecisionStatus.Published, review.Decision!.Status);
    }

    [Fact]
    public async Task Phase20Permission_CancelledPrompt_ReturnsCancelledOutcome()
    {
        var bridge = new AcpClientPermissionBridge(
            "fake-session-1",
            new CancellingPermissionChoiceSource());

        var response = await bridge.HandleAsync(
            CreatePermissionRequest("allow_once"),
            CancellationToken.None);

        Assert.True(response.IsSuccess);
        var wire = AcpMessageCodec.DeserializeResult<AcpRequestPermissionResponseWire>(response.Result);
        Assert.Equal("cancelled", wire.Outcome.Outcome);
    }

    private ContractAgentActionBroker CreateBroker(
        IAgentPermissionReviewService reviewService,
        IAgentFileReader? fileReader = null) =>
        new(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            ActorId.FromValue("actor:user"),
            ActorId.FromValue("actor:agent"),
            AgentBackendIds.Acp,
            new FakeWorkspaceActionAuthority(_scope),
            fileReader ?? new WorkspaceFileReader(),
            new WorkspaceFileMutator(),
            new FakeTrustedCommandResolver(),
            new WorkspaceCommandExecutor(),
            new AgentActionRunSlotTracker(),
            new AgentActionCorrelationRegistry(),
            reviewService);

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

    private static AcpJsonRpcRequest CreatePermissionRequest(string selectedKind) =>
        CreateRequest(
            AcpMethodNames.SessionRequestPermission,
            new AcpRequestPermissionRequestWire
            {
                SessionId = "fake-session-1",
                ToolCall = new AcpToolCallWire
                {
                    ToolCallId = "tool-1",
                    Title = "write_file",
                },
                Options =
                [
                    new AcpPermissionOptionWire
                    {
                        OptionId = "allow",
                        Name = "Allow once",
                        Kind = selectedKind,
                    },
                    new AcpPermissionOptionWire
                    {
                        OptionId = "reject",
                        Name = "Reject once",
                        Kind = "reject_once",
                    },
                ],
            });

    private static AcpJsonRpcRequest CreateRequest(string method, object parameters) =>
        new()
        {
            Id = AcpJsonRpcRequestId.FromNumber(1),
            Method = method,
            Params = System.Text.Json.JsonSerializer.SerializeToElement(
                parameters,
                AcpJsonSerializerOptionsFactory.SharedOptions),
        };

    private static AgentFileReadResult ConfirmedAbsentTarget =>
        AgentFileReadResult.Rejected(
            AgentFileReadOutcome.NotFound,
            "File does not exist in the workspace.");

    private sealed class CapturingAllowingPermissionReviewService : IAgentPermissionReviewService
    {
        public AgentPermissionDecision? Decision { get; private set; }

        public ValueTask<AgentPermissionDecision> RequestDecisionAsync(
            AgentActionRequest request,
            AgentActionDisplaySummary displaySummary,
            WorkspaceActionScope? workspaceScope,
            CancellationToken cancellationToken)
        {
            Decision ??= new AgentPermissionDecision(
                AgentPermissionDecisionId.New(),
                request.Fingerprint,
                AgentActionPermissionClassification.RequiresUserDecision,
                AgentPermissionDecisionStatus.Published,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                true);
            return ValueTask.FromResult(Decision);
        }
    }

    private sealed class SelectingPermissionChoiceSource(string selectedKind) : IAcpPermissionChoiceSource
    {
        public ValueTask<AcpRequestPermissionResponseWire> ChooseAsync(
            AcpRequestPermissionRequestWire request,
            CancellationToken cancellationToken)
        {
            var selected = request.Options[0];
            foreach (var option in request.Options)
            {
                if (string.Equals(option.Kind, selectedKind, StringComparison.Ordinal))
                {
                    selected = option;
                    break;
                }
            }

            return ValueTask.FromResult(new AcpRequestPermissionResponseWire
            {
                Outcome = new AcpRequestPermissionOutcomeWire
                {
                    Outcome = "selected",
                    OptionId = selected.OptionId,
                },
            });
        }
    }

    private sealed class CancellingPermissionChoiceSource : IAcpPermissionChoiceSource
    {
        public ValueTask<AcpRequestPermissionResponseWire> ChooseAsync(
            AcpRequestPermissionRequestWire request,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AcpRequestPermissionResponseWire
            {
                Outcome = new AcpRequestPermissionOutcomeWire
                {
                    Outcome = "cancelled",
                },
            });
    }
}
