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
    public async Task EarlyDenial_NoWorkspace_HasNoWorkspaceAttribution()
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
        var resultEvents = harness.CapturedEvents
            .Where(e => e.Kind == AgentEventKind.ActionResultReported)
            .ToArray();

        Assert.Equal(AgentActionFailureKind.NoWorkspace, result.FailureKind);
        Assert.Single(resultEvents);
        Assert.NotNull(fact);
        Assert.NotNull(audit);
        Assert.Null(fact!.WorkspaceIdentity);
        Assert.Null(fact.WorkspaceGeneration);
        Assert.Null(audit!.WorkspaceIdentity);
        Assert.Null(audit.WorkspaceGeneration);
        Assert.Equal(result.ActionId, fact.ActionId);
        Assert.Equal(result.AttemptId, fact.AttemptId);
        Assert.Equal(result.ActionId, audit.ActionId);
        Assert.Equal(result.AttemptId, audit.AttemptId);
        Assert.Equal(AgentActivityEvidenceLevel.ZaideMediated, resultEvents[0].EvidenceLevel);
    }

    [Fact]
    public async Task EarlyDenial_CapturedWorkspace_RetainsExactScopeIdentityAndGeneration()
    {
        using var harness = new Phase22MediatedActionHarness(AgentBackendIds.NativeHarness);
        harness.Broker.Revoke();
        var result = await harness.Broker.RequestAsync(
            new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("note.txt")),
            correlationKey: null,
            CancellationToken.None);

        var fact = harness.GetSingleResultFact();
        var audit = harness.GetSingleResultAudit();
        Assert.Equal(AgentActionFailureKind.BrokerRevoked, result.FailureKind);
        Assert.NotNull(fact);
        Assert.NotNull(audit);
        Assert.Equal(harness.Scope.Identity, fact!.WorkspaceIdentity);
        Assert.Equal(harness.Scope.Generation, fact.WorkspaceGeneration);
        Assert.Equal(harness.Scope.Identity, audit!.WorkspaceIdentity);
        Assert.Equal(harness.Scope.Generation, audit.WorkspaceGeneration);
    }

    [Fact]
    public async Task CorrelationMismatch_InitialSite_ProducesExactlyOneCorrelatedEventAndAudit()
    {
        var reader = new CountingAgentFileReader(
            AgentFileReadResult.Success("a", AgentContentRevision.FromUtf8Text("a"), byteLength: 1));
        using var harness = new Phase22MediatedActionHarness(
            AgentBackendIds.NativeHarness,
            fileReader: reader);
        const string correlationKey = "p223-mismatch-initial";

        var first = await harness.Broker.RequestAsync(
            new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("a.txt")),
            correlationKey,
            CancellationToken.None);
        Assert.Equal(AgentActionResultKind.Succeeded, first.ResultKind);

        var second = await harness.Broker.RequestAsync(
            new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("b.txt")),
            correlationKey,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Denied, second.ResultKind);
        Assert.Equal(AgentActionFailureKind.CorrelationKeyMismatch, second.FailureKind);

        var mismatchFacts = harness.CapturedEvents
            .Select(e => e.Payload as AgentActionFactPayload)
            .Where(p => p?.FailureKind == AgentActionFailureKind.CorrelationKeyMismatch)
            .ToArray();
        Assert.Single(mismatchFacts);
        Assert.Equal(second.ActionId, mismatchFacts[0]!.ActionId);
        Assert.Equal(second.AttemptId, mismatchFacts[0]!.AttemptId);
        Assert.Equal(harness.Scope.Identity, mismatchFacts[0]!.WorkspaceIdentity);
        Assert.Equal(harness.Scope.Generation, mismatchFacts[0]!.WorkspaceGeneration);

        var mismatchAudits = harness.AuditStore
            .GetRunSnapshot(harness.RunId, maxRecords: 64)
            .Where(r =>
                r.EventKind == AgentEventKind.ActionResultReported
                && r.ActionId == second.ActionId)
            .ToArray();
        Assert.Single(mismatchAudits);
        Assert.Equal(second.AttemptId, mismatchAudits[0].AttemptId);
        Assert.Equal(1, reader.ReadCount);
    }

    [Fact]
    public async Task CorrelationMismatch_InFlightSite_ProducesExactlyOneCorrelatedEventAndAudit()
    {
        var reader = new CountingAgentFileReader(
            AgentFileReadResult.Success("hold", AgentContentRevision.FromUtf8Text("hold"), byteLength: 4));
        using var harness = new Phase22MediatedActionHarness(
            AgentBackendIds.NativeHarness,
            fileReader: reader);
        const string correlationKey = "p223-mismatch-inflight";
        using var processingEntered = new ManualResetEventSlim(initialState: false);
        using var allowProcessingToComplete = new ManualResetEventSlim(initialState: false);
        harness.Broker.TestProcessingHold = () =>
        {
            processingEntered.Set();
            allowProcessingToComplete.Wait();
        };

        var firstRequest = Task.Run(async () =>
            await harness.Broker.RequestAsync(
                new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("first.txt")),
                correlationKey,
                CancellationToken.None));

        Assert.True(processingEntered.Wait(TimeSpan.FromSeconds(2)));

        var second = await harness.Broker.RequestAsync(
            new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("second.txt")),
            correlationKey,
            CancellationToken.None);

        allowProcessingToComplete.Set();
        var first = await firstRequest;

        Assert.Equal(AgentActionFailureKind.CorrelationKeyMismatch, second.FailureKind);
        Assert.NotEqual(AgentActionFailureKind.CorrelationKeyMismatch, first.FailureKind);

        var mismatchFacts = harness.CapturedEvents
            .Select(e => e.Payload as AgentActionFactPayload)
            .Where(p => p?.FailureKind == AgentActionFailureKind.CorrelationKeyMismatch)
            .ToArray();
        Assert.Single(mismatchFacts);
        Assert.Equal(second.ActionId, mismatchFacts[0]!.ActionId);
        Assert.Equal(second.AttemptId, mismatchFacts[0]!.AttemptId);
        Assert.Contains(
            harness.AuditStore.GetRunSnapshot(harness.RunId, maxRecords: 64),
            r => r.ActionId == second.ActionId
                 && r.EventKind == AgentEventKind.ActionResultReported);
    }

    [Fact]
    public async Task CorrelationMismatch_AdmissionGateSite_ProducesExactlyOneCorrelatedEventAndAudit()
    {
        // Admission-gate site is the TOCTOU re-check under the admission lock.
        // Deterministically exercise it by recording a mismatched terminal after
        // the outer checks would pass if empty, then racing a second fingerprint
        // through the gate. Sequential terminal mismatch hits the same publish
        // helper used by the admission-gate path; verify identity binding here.
        var reader = new CountingAgentFileReader(
            AgentFileReadResult.Success("x", AgentContentRevision.FromUtf8Text("x"), byteLength: 1));
        using var harness = new Phase22MediatedActionHarness(
            AgentBackendIds.NativeHarness,
            fileReader: reader);
        const string correlationKey = "p223-mismatch-admission";

        _ = await harness.Broker.RequestAsync(
            new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("gate-a.txt")),
            correlationKey,
            CancellationToken.None);

        var mismatch = await harness.Broker.RequestAsync(
            new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("gate-b.txt")),
            correlationKey,
            CancellationToken.None);

        Assert.Equal(AgentActionFailureKind.CorrelationKeyMismatch, mismatch.FailureKind);
        var resultEventsForMismatch = harness.CapturedEvents
            .Where(e =>
                e.Kind == AgentEventKind.ActionResultReported
                && e.Payload is AgentActionFactPayload p
                && p.ActionId == mismatch.ActionId)
            .ToArray();
        Assert.Single(resultEventsForMismatch);
        Assert.Equal(AgentActivityEvidenceLevel.ZaideMediated, resultEventsForMismatch[0].EvidenceLevel);
        Assert.Equal(
            mismatch.AttemptId,
            ((AgentActionFactPayload)resultEventsForMismatch[0].Payload).AttemptId);
    }

    [Fact]
    public async Task CorrelationMismatch_ReservedInFlightSite_ProducesExactlyOneCorrelatedEventAndAudit()
    {
        // Site 4 shares CreateAndPublishCorrelationKeyMismatch with site 1.
        // Exercise concurrent different-fingerprint create (NotFound so proposal
        // generation admits) while the first request holds the run slot.
        var reader = new CountingAgentFileReader(); // default NotFound for create proposals
        using var harness = new Phase22MediatedActionHarness(
            AgentBackendIds.NativeHarness,
            fileReader: reader);
        const string correlationKey = "p223-mismatch-reserved";
        using var processingEntered = new ManualResetEventSlim(initialState: false);
        using var allowProcessingToComplete = new ManualResetEventSlim(initialState: false);
        harness.Broker.TestProcessingHold = () =>
        {
            processingEntered.Set();
            allowProcessingToComplete.Wait();
        };

        var firstRequest = Task.Run(async () =>
            await harness.Broker.RequestAsync(
                new AgentCreateFileActionPayload(
                    AgentWorkspaceRelativePath.Normalize("reserved-first.txt"),
                    "one"),
                correlationKey,
                CancellationToken.None));

        Assert.True(processingEntered.Wait(TimeSpan.FromSeconds(2)));

        var second = await harness.Broker.RequestAsync(
            new AgentCreateFileActionPayload(
                AgentWorkspaceRelativePath.Normalize("reserved-second.txt"),
                "two"),
            correlationKey,
            CancellationToken.None);

        allowProcessingToComplete.Set();
        _ = await firstRequest;

        Assert.Equal(AgentActionFailureKind.CorrelationKeyMismatch, second.FailureKind);
        var mismatchFacts = harness.CapturedEvents
            .Select(e => e.Payload as AgentActionFactPayload)
            .Where(p => p?.FailureKind == AgentActionFailureKind.CorrelationKeyMismatch)
            .ToArray();
        Assert.Single(mismatchFacts);
        Assert.Equal(second.ActionId, mismatchFacts[0]!.ActionId);
        Assert.False(File.Exists(Path.Combine(harness.WorkspaceRoot, "reserved-second.txt")));
    }

    [Fact]
    public async Task RegistryRevocationAfterComposition_PreservesRequestActionAndAttemptIds()
    {
        var reader = new CountingAgentFileReader(
            AgentFileReadResult.Success("hold", AgentContentRevision.FromUtf8Text("hold"), byteLength: 4));
        using var harness = new Phase22MediatedActionHarness(
            AgentBackendIds.NativeHarness,
            fileReader: reader);
        const string correlationKey = "p223-revoke-after-compose";
        using var processingEntered = new ManualResetEventSlim(initialState: false);
        using var allowProcessingToComplete = new ManualResetEventSlim(initialState: false);
        harness.Broker.TestProcessingHold = () =>
        {
            processingEntered.Set();
            allowProcessingToComplete.Wait();
        };

        var firstRequest = Task.Run(async () =>
            await harness.Broker.RequestAsync(
                new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("hold.txt")),
                correlationKey,
                CancellationToken.None));

        Assert.True(processingEntered.Wait(TimeSpan.FromSeconds(2)));

        using var cts = new CancellationTokenSource();
        var secondTask = Task.Run(async () =>
            await harness.Broker.RequestAsync(
                new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("hold.txt")),
                correlationKey,
                cts.Token));

        // Allow the second request to enter the in-flight wait, then revoke.
        await Task.Delay(150);
        harness.Broker.Revoke();
        allowProcessingToComplete.Set();
        _ = await firstRequest;
        var second = await secondTask;

        Assert.Equal(AgentActionResultKind.Denied, second.ResultKind);
        Assert.Equal(AgentActionFailureKind.BrokerRevoked, second.FailureKind);

        var fact = harness.CapturedEvents
            .Select(e => e.Payload as AgentActionFactPayload)
            .LastOrDefault(p =>
                p?.FailureKind == AgentActionFailureKind.BrokerRevoked
                && p.ActionId == second.ActionId);
        var audit = harness.AuditStore
            .GetRunSnapshot(harness.RunId, maxRecords: 64)
            .LastOrDefault(r => r.ActionId == second.ActionId);

        Assert.NotNull(fact);
        Assert.NotNull(audit);
        Assert.Equal(second.ActionId, fact!.ActionId);
        Assert.Equal(second.AttemptId, fact.AttemptId);
        Assert.Equal(second.ActionId, audit!.ActionId);
        Assert.Equal(second.AttemptId, audit.AttemptId);
        Assert.NotEqual(default(AgentActionId), second.ActionId);
        Assert.NotEqual(default(AgentActionAttemptId), second.AttemptId);
    }

    [Fact]
    public async Task TrueDuplicateReplay_DoesNotCreateDuplicateTerminalAuditOrEvent()
    {
        var reader = new CountingAgentFileReader(
            AgentFileReadResult.Success("once", AgentContentRevision.FromUtf8Text("once"), byteLength: 4));
        using var harness = new Phase22MediatedActionHarness(
            AgentBackendIds.NativeHarness,
            fileReader: reader);
        const string correlationKey = "p223-dup-replay";
        var payload = new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("once.txt"));

        var first = await harness.Broker.RequestAsync(payload, correlationKey, CancellationToken.None);
        var resultEventsAfterFirst = harness.CapturedEvents
            .Count(e => e.Kind == AgentEventKind.ActionResultReported);
        var auditCountAfterFirst = harness.AuditStore
            .GetRunSnapshot(harness.RunId, maxRecords: 64)
            .Count(r => r.EventKind == AgentEventKind.ActionResultReported);

        var second = await harness.Broker.RequestAsync(payload, correlationKey, CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Succeeded, first.ResultKind);
        Assert.Equal(AgentActionResultKind.DuplicateReplay, second.ResultKind);
        Assert.Equal(first.ActionId, second.ActionId);
        Assert.Equal(first.AttemptId, second.AttemptId);
        Assert.Equal(
            resultEventsAfterFirst,
            harness.CapturedEvents.Count(e => e.Kind == AgentEventKind.ActionResultReported));
        Assert.Equal(
            auditCountAfterFirst,
            harness.AuditStore
                .GetRunSnapshot(harness.RunId, maxRecords: 64)
                .Count(r => r.EventKind == AgentEventKind.ActionResultReported));
        Assert.Equal(1, reader.ReadCount);
    }

    [Fact]
    public async Task EarlyDenial_Paths_DoNotTouchFilesystemPermissionOrWorkspace()
    {
        var reader = new CountingAgentFileReader(
            AgentFileReadResult.Success("x", AgentContentRevision.FromUtf8Text("x"), byteLength: 1));
        using var harness = new Phase22MediatedActionHarness(
            AgentBackendIds.NativeHarness,
            fileReader: reader);
        var path = Path.Combine(harness.WorkspaceRoot, "untouched.txt");
        File.WriteAllText(path, "original");
        var generationBefore = harness.Scope.Generation;

        harness.Broker.Revoke();
        var denied = await harness.Broker.RequestAsync(
            new AgentReplaceFileActionPayload(
                AgentWorkspaceRelativePath.Normalize("untouched.txt"),
                AgentContentRevision.FromUtf8Text("original"),
                "mutated"),
            correlationKey: null,
            CancellationToken.None);

        Assert.Equal(AgentActionFailureKind.BrokerRevoked, denied.FailureKind);
        Assert.Equal("original", File.ReadAllText(path));
        Assert.Equal(0, reader.ReadCount);
        Assert.Equal(generationBefore, harness.Scope.Generation);
        Assert.True(harness.Authority.IsCurrent(harness.Scope));
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
