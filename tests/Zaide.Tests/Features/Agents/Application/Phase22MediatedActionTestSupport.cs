using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Application.Acp;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Agents.Infrastructure.Acp;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Domain;
using Zaide.Tests.Features.Agents.Acp.Backend;
using Zaide.Tests.Features.Conversations;

namespace Zaide.Tests.Features.Agents.Application;

internal sealed class Phase22MediatedActionHarness : IDisposable
{
    private readonly object _sequenceSync = new();
    private long _sequence;

    public Phase22MediatedActionHarness(
        AgentBackendId backendId,
        IAgentPermissionReviewService? reviewService = null,
        IAgentFileReader? fileReader = null,
        IAgentFileMutator? fileMutator = null,
        IAgentDocumentReconciler? documentReconciler = null,
        ActorId? initiatingActorId = null,
        ActorId? targetActorId = null,
        bool hasWorkspace = true)
    {
        WorkspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "zaide-p223-m3-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(WorkspaceRoot);
        Scope = FakeWorkspaceActionAuthority.CreateScopeFromDirectory(WorkspaceRoot);
        Authority = new FakeWorkspaceActionAuthority(Scope) { HasWorkspace = hasWorkspace };
        SessionId = AgentSessionId.New();
        RunId = ExecutionRunId.New();
        ConversationId = ConversationId.NewDirect();
        InitiatingActorId = initiatingActorId ?? ActorId.FromValue("actor:human");
        TargetActorId = targetActorId ?? ActorId.FromValue("actor:target");
        BackendId = backendId;
        AuditStore = new AgentActionAuditStore();
        EventStream = new AgentEventStream();
        CapturedEvents = new List<AgentEvent>();
        EventStream.Events.Subscribe(CapturedEvents.Add);

        var publisher = new RunScopedAgentActionEventPublisher(
            SessionId,
            RunId,
            ConversationId,
            BackendId,
            InitiatingActorId,
            TargetActorId,
            EventStream,
            AuditStore,
            () =>
            {
                lock (_sequenceSync)
                {
                    return ++_sequence;
                }
            },
            _sequenceSync);

        Broker = new ContractAgentActionBroker(
            SessionId,
            RunId,
            ConversationId,
            InitiatingActorId,
            TargetActorId,
            BackendId,
            Authority,
            fileReader ?? new WorkspaceFileReader(),
            fileMutator ?? new WorkspaceFileMutator(),
            new DefaultAgentCommandResolver(),
            new WorkspaceCommandExecutor(),
            new AgentActionRunSlotTracker(),
            new AgentActionCorrelationRegistry(),
            reviewService ?? new AllowingPermissionReviewService(),
            documentReconciler ?? NullAgentDocumentReconciler.Instance,
            publisher);
    }

    public string WorkspaceRoot { get; }

    public WorkspaceActionScope Scope { get; }

    public FakeWorkspaceActionAuthority Authority { get; }

    public AgentSessionId SessionId { get; }

    public ExecutionRunId RunId { get; }

    public ConversationId ConversationId { get; }

    public ActorId InitiatingActorId { get; }

    public ActorId TargetActorId { get; }

    public AgentBackendId BackendId { get; }

    public AgentActionAuditStore AuditStore { get; }

    public AgentEventStream EventStream { get; }

    public List<AgentEvent> CapturedEvents { get; }

    public ContractAgentActionBroker Broker { get; }

    public AgentBackendExecutionContext CreateBackendContext(string message = "phase-22.3 mediated action") =>
        new(
            new AgentBackendRequest(
                SessionId,
                RunId,
                ConversationId,
                InitiatingActorId,
                TargetActorId,
                ConversationEntryId.New(),
                message),
            Broker);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(WorkspaceRoot))
            {
                Directory.Delete(WorkspaceRoot, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    public AgentActionFactPayload? GetLastResultFact() =>
        CapturedEvents
            .Select(e => e.Payload as AgentActionFactPayload)
            .LastOrDefault(p => p?.ResultKind is not null);

    public AgentActionFactPayload? GetResultFact(AgentActionKind actionKind) =>
        CapturedEvents
            .Select(e => e.Payload as AgentActionFactPayload)
            .LastOrDefault(p => p?.ResultKind is not null && p.ActionKind == actionKind);

    public AgentActionFactPayload? GetSingleResultFact() =>
        GetLastResultFact();

    public AgentActionAuditRecord? GetSingleResultAudit() =>
        AuditStore
            .GetRunSnapshot(RunId, maxRecords: 64)
            .LastOrDefault(record => record.EventKind == AgentEventKind.ActionResultReported);
}

internal static class Phase22MediatedActionTestSupport
{
    public static async Task<IReadOnlyList<AgentBackendEvent>> CollectNativeHarnessEventsAsync(
        ScriptedNativeHarnessProviderTransport transport,
        Phase22MediatedActionHarness harness,
        params NativeHarnessProviderResponse[] responses)
    {
        foreach (var response in responses)
        {
            transport.Enqueue(response);
        }

        var backend = new NativeHarnessAgentBackend(
            Phase19HarnessTestFactory.CreateExecutionService(harness.WorkspaceRoot),
            transport,
            new NativeHarnessPriorConversationReader(ConversationsTestSupport.CreateStore()));

        var events = new List<AgentBackendEvent>();
        await foreach (var backendEvent in backend.ExecuteAsync(
                           harness.CreateBackendContext(),
                           CancellationToken.None))
        {
            events.Add(backendEvent);
        }

        return events;
    }

    public static async Task<IReadOnlyList<AgentBackendEvent>> CollectAcpEventsAsync(
        AcpFakeSessionScript script,
        Phase22MediatedActionHarness harness)
    {
        var backend = new AcpActionCapableAgentBackend(
            new DelegatingAcpSessionClientFactory(
                _ => Task.FromResult<IAcpSessionClient>(new AcpFakeSessionClient(script))),
            () => harness.WorkspaceRoot);

        var events = new List<AgentBackendEvent>();
        await foreach (var backendEvent in backend.ExecuteAsync(
                           harness.CreateBackendContext(),
                           CancellationToken.None))
        {
            events.Add(backendEvent);
        }

        return events;
    }

    public static AcpFakeInboundRequest CreateAcpReadRequest(string absolutePath) =>
        new()
        {
            Request = CreateAcpRequest(
                AcpMethodNames.FsReadTextFile,
                new AcpReadTextFileRequestWire
                {
                    SessionId = "fake-session-1",
                    Path = absolutePath,
                }),
            ResponseCallback = _ => { },
        };

    public static AcpFakeInboundRequest CreateAcpWriteRequest(string absolutePath, string content) =>
        new()
        {
            Request = CreateAcpRequest(
                AcpMethodNames.FsWriteTextFile,
                new AcpWriteTextFileRequestWire
                {
                    SessionId = "fake-session-1",
                    Path = absolutePath,
                    Content = content,
                }),
            ResponseCallback = _ => { },
        };

    public static AcpJsonRpcRequest CreateAcpRequest(string method, object parameters) =>
        new()
        {
            Id = AcpJsonRpcRequestId.FromNumber(1),
            Method = method,
            Params = System.Text.Json.JsonSerializer.SerializeToElement(
                parameters,
                AcpJsonSerializerOptionsFactory.SharedOptions),
        };

    public static NativeHarnessProviderResponse ToolCallThenComplete(
        string toolName,
        string argumentsJson,
        string assistantText = "done")
    {
        _ = assistantText;
        return NativeHarnessProviderResponse.Success(
            assistantContent: null,
            toolCalls:
            [
                new NativeHarnessProviderToolCall(
                    NativeHarnessToolCallId.FromValue("call-1"),
                    toolName,
                    argumentsJson),
            ]);
    }

    public static NativeHarnessProviderResponse Complete(string assistantText = "done") =>
        NativeHarnessProviderResponse.Success(assistantText);
}

internal sealed class AllowingPermissionReviewService : IAgentPermissionReviewService
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

internal sealed class DenyingPermissionReviewService : IAgentPermissionReviewService
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
            AgentPermissionDecisionStatus.Denied,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(5),
            false));
}

internal sealed class DismissingPermissionReviewService : IAgentPermissionReviewService
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
            AgentPermissionDecisionStatus.Denied,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(5),
            isAllow: false));
}

internal sealed class ExpiredPermissionReviewService : IAgentPermissionReviewService
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
            DateTimeOffset.UtcNow.AddMinutes(-10),
            DateTimeOffset.UtcNow.AddMinutes(-1),
            true));
}

internal sealed class CapturingAllowingPermissionReviewService : IAgentPermissionReviewService
{
    private readonly object _gate = new();

    public AgentPermissionDecision? Decision { get; private set; }

    public ValueTask<AgentPermissionDecision> RequestDecisionAsync(
        AgentActionRequest request,
        AgentActionDisplaySummary displaySummary,
        WorkspaceActionScope? workspaceScope,
        CancellationToken cancellationToken)
    {
        lock (_gate)
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
}

internal sealed class UnavailablePermissionReviewService : IAgentPermissionReviewService
{
    public ValueTask<AgentPermissionDecision> RequestDecisionAsync(
        AgentActionRequest request,
        AgentActionDisplaySummary displaySummary,
        WorkspaceActionScope? workspaceScope,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Permission review unavailable.");
}
