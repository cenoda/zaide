using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Agents.Infrastructure.Acp;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Domain;
using Zaide.Tests.Features.Agents.Acp.Backend;

namespace Zaide.Tests.Features.Agents.Application;

public sealed class Phase22ActionAttributionTests
{
    [Fact]
    public async Task ActionFact_IncludesInitiatingAndTargetActorAttribution()
    {
        var initiating = ActorId.FromValue("actor:initiator");
        var target = ActorId.FromValue("actor:target");
        using var harness = new Phase22MediatedActionHarness(
            AgentBackendIds.NativeHarness,
            initiatingActorId: initiating,
            targetActorId: target);
        File.WriteAllText(Path.Combine(harness.WorkspaceRoot, "note.txt"), "hello");

        var transport = new ScriptedNativeHarnessProviderTransport();
        await Phase22MediatedActionTestSupport.CollectNativeHarnessEventsAsync(
            transport,
            harness,
            Phase22MediatedActionTestSupport.ToolCallThenComplete(
                NativeHarnessProviderProtocol.ReadFileToolName,
                """{"path":"note.txt"}"""),
            Phase22MediatedActionTestSupport.Complete());

        var fact = harness.GetSingleResultFact();
        Assert.NotNull(fact);
        Assert.Equal(initiating, fact!.InitiatingActorId);
        Assert.Equal(target, fact.TargetActorId);
    }

    [Fact]
    public async Task AuditRecord_IncludesInitiatingAndTargetActorAttribution()
    {
        var initiating = ActorId.FromValue("actor:initiator");
        var target = ActorId.FromValue("actor:target");
        using var harness = new Phase22MediatedActionHarness(
            AgentBackendIds.Acp,
            initiatingActorId: initiating,
            targetActorId: target);
        var absolutePath = Path.Combine(harness.WorkspaceRoot, "audit.txt");
        File.WriteAllText(absolutePath, "audit");
        var script = new AcpFakeSessionScript
        {
            InboundRequestsDuringPrompt =
            [
                Phase22MediatedActionTestSupport.CreateAcpReadRequest(absolutePath),
            ],
        };

        await Phase22MediatedActionTestSupport.CollectAcpEventsAsync(script, harness);

        var audit = harness.GetSingleResultAudit();
        Assert.NotNull(audit);
        Assert.Equal(initiating, audit!.InitiatingActorId);
        Assert.Equal(target, audit.TargetActorId);
        Assert.Equal(harness.SessionId, audit.SessionId);
        Assert.Equal(harness.RunId, audit.RunId);
        Assert.Equal(harness.ConversationId, audit.ConversationId);
        Assert.Equal(AgentBackendIds.Acp, audit.BackendId);
    }

    [Fact]
    public async Task EarlyDenial_BrokerRevoked_ProducesCorrelatedEventAndAudit()
    {
        using var harness = new Phase22MediatedActionHarness(AgentBackendIds.NativeHarness);
        harness.Broker.Revoke();
        var result = await harness.Broker.RequestAsync(
            new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("note.txt")),
            correlationKey: null,
            CancellationToken.None);

        var fact = harness.GetSingleResultFact();
        var audit = harness.GetSingleResultAudit();
        Assert.NotNull(fact);
        Assert.NotNull(audit);
        Assert.Equal(result.ActionId, fact!.ActionId);
        Assert.Equal(result.AttemptId, fact.AttemptId);
        Assert.Equal(fact.ActionId, audit!.ActionId);
        Assert.Equal(AgentActionFailureKind.BrokerRevoked, fact.FailureKind);
    }

    [Fact]
    public async Task EarlyDenial_NoWorkspace_ProducesCorrelatedEventAndAudit()
    {
        using var harness = new Phase22MediatedActionHarness(
            AgentBackendIds.NativeHarness,
            hasWorkspace: false);
        var result = await harness.Broker.RequestAsync(
            new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("note.txt")),
            correlationKey: null,
            CancellationToken.None);

        var fact = harness.GetSingleResultFact();
        var audit = harness.GetSingleResultAudit();
        Assert.NotNull(fact);
        Assert.NotNull(audit);
        Assert.Equal(result.ActionId, fact!.ActionId);
        Assert.Equal(AgentActionFailureKind.NoWorkspace, fact.FailureKind);
        Assert.Equal(harness.InitiatingActorId, fact.InitiatingActorId);
        Assert.Equal(harness.TargetActorId, fact.TargetActorId);
    }

    [Fact]
    public async Task EarlyDenial_ConcurrentAction_ProducesCorrelatedEventAndAudit()
    {
        using var harness = new Phase22MediatedActionHarness(AgentBackendIds.NativeHarness);
        File.WriteAllText(Path.Combine(harness.WorkspaceRoot, "note.txt"), "hello");
        using var processingEntered = new ManualResetEventSlim(initialState: false);
        using var allowProcessingToComplete = new ManualResetEventSlim(initialState: false);
        harness.Broker.TestProcessingHold = () =>
        {
            processingEntered.Set();
            allowProcessingToComplete.Wait();
        };

        var firstRequest = Task.Run(async () =>
            await harness.Broker.RequestAsync(
                new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("note.txt")),
                correlationKey: null,
                CancellationToken.None));

        Assert.True(processingEntered.Wait(TimeSpan.FromSeconds(1)));
        var secondResult = await harness.Broker.RequestAsync(
            new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("other.txt")),
            correlationKey: null,
            CancellationToken.None);
        allowProcessingToComplete.Set();
        _ = await firstRequest;

        var denialFacts = harness.CapturedEvents
            .Select(e => e.Payload as AgentActionFactPayload)
            .Where(p => p?.FailureKind == AgentActionFailureKind.ConcurrentActionRejected)
            .ToArray();
        Assert.Single(denialFacts);
        Assert.Equal(secondResult.ActionId, denialFacts[0]!.ActionId);
        Assert.Contains(
            harness.AuditStore.GetRunSnapshot(harness.RunId, maxRecords: 64),
            record => record.ActionId == secondResult.ActionId);
    }

    [Fact]
    public async Task Attribution_PreservesSessionRunConversationBackendActionWorkspace()
    {
        using var harness = new Phase22MediatedActionHarness(AgentBackendIds.NativeHarness);
        File.WriteAllText(Path.Combine(harness.WorkspaceRoot, "dims.txt"), "dims");
        var transport = new ScriptedNativeHarnessProviderTransport();
        await Phase22MediatedActionTestSupport.CollectNativeHarnessEventsAsync(
            transport,
            harness,
            Phase22MediatedActionTestSupport.ToolCallThenComplete(
                NativeHarnessProviderProtocol.ReadFileToolName,
                """{"path":"dims.txt"}"""),
            Phase22MediatedActionTestSupport.Complete());

        var fact = harness.GetSingleResultFact();
        var audit = harness.GetSingleResultAudit();
        var resultEvent = harness.CapturedEvents.Single(
            e => e.Kind == AgentEventKind.ActionResultReported);

        Assert.Equal(harness.SessionId, resultEvent.SessionId);
        Assert.Equal(harness.RunId, resultEvent.RunId);
        Assert.Equal(harness.ConversationId, resultEvent.ConversationId);
        Assert.Equal(AgentBackendIds.NativeHarness, resultEvent.BackendId);
        Assert.Equal(harness.Scope.Identity, fact!.WorkspaceIdentity);
        Assert.Equal(harness.Scope.Generation, fact.WorkspaceGeneration);
        Assert.Equal(AgentActionKind.ReadFile, fact.ActionKind);
        Assert.Equal(harness.RunId, audit!.RunId);
    }

    [Fact]
    public void Redaction_PreservesBoundedAuditSummary()
    {
        var summary = new AgentActionAuditSummary("api_key=super-secret " + new string('x', 4096));
        Assert.True(summary.WasRedacted);
        Assert.DoesNotContain("super-secret", summary.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Retention_PreservesBoundedAuditStoreCap()
    {
        var store = new AgentActionAuditStore();
        for (var i = 0; i < 266; i++)
        {
            store.Record(new AgentActionAuditRecord(
                AgentEventId.New(),
                AgentEventKind.ActionResultReported,
                AgentSessionId.New(),
                ExecutionRunId.New(),
                ConversationId.NewDirect(),
                AgentBackendIds.NativeHarness,
                ActorId.HumanUser,
                ActorId.TownhallAgent,
                i + 1,
                DateTimeOffset.UtcNow.AddMilliseconds(i),
                AgentActivityEvidenceLevel.ZaideMediated,
                AgentActionId.New(),
                AgentActionAttemptId.New(),
                AgentActionKind.ReadFile,
                WorkspaceIdentity.New(),
                WorkspaceGeneration.Initial,
                new AgentActionAuditSummary("bounded")));
        }

        Assert.Equal(256, store.GetCurrentLifetimeSnapshot(maxRecords: 1000).Count);
    }

    [Fact]
    public async Task MalformedPayload_DeniedWithoutMutation()
    {
        using var harness = new Phase22MediatedActionHarness(AgentBackendIds.NativeHarness);
        var path = Path.Combine(harness.WorkspaceRoot, "exists.txt");
        File.WriteAllText(path, "already there");
        var result = await harness.Broker.RequestAsync(
            new AgentCreateFileActionPayload(
                AgentWorkspaceRelativePath.Normalize("exists.txt"),
                "duplicate"),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Denied, result.ResultKind);
        Assert.Equal("already there", File.ReadAllText(path));
        Assert.NotNull(harness.GetSingleResultFact());
    }

    [Fact]
    public async Task SiblingBackends_DoNotWrapFallbackOrCrossRetry()
    {
        using var nativeHarness = new Phase22MediatedActionHarness(AgentBackendIds.NativeHarness);
        using var acpHarness = new Phase22MediatedActionHarness(AgentBackendIds.Acp);
        File.WriteAllText(Path.Combine(nativeHarness.WorkspaceRoot, "native.txt"), "native");
        File.WriteAllText(Path.Combine(acpHarness.WorkspaceRoot, "acp.txt"), "acp");

        var nativeTransport = new ScriptedNativeHarnessProviderTransport();
        await Phase22MediatedActionTestSupport.CollectNativeHarnessEventsAsync(
            nativeTransport,
            nativeHarness,
            Phase22MediatedActionTestSupport.ToolCallThenComplete(
                NativeHarnessProviderProtocol.ReadFileToolName,
                """{"path":"native.txt"}"""),
            Phase22MediatedActionTestSupport.Complete());

        var acpScript = new AcpFakeSessionScript
        {
            InboundRequestsDuringPrompt =
            [
                Phase22MediatedActionTestSupport.CreateAcpReadRequest(
                    Path.Combine(acpHarness.WorkspaceRoot, "acp.txt")),
            ],
        };
        await Phase22MediatedActionTestSupport.CollectAcpEventsAsync(acpScript, acpHarness);

        Assert.Equal(AgentBackendIds.NativeHarness, nativeHarness.GetSingleResultAudit()!.BackendId);
        Assert.Equal(AgentBackendIds.Acp, acpHarness.GetSingleResultAudit()!.BackendId);
        Assert.NotEqual(
            nativeHarness.GetSingleResultAudit()!.ActionId,
            acpHarness.GetSingleResultAudit()!.ActionId);
    }
}
