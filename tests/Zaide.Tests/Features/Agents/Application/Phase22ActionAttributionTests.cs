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
        // Mismatch is registered before RequestAsync reaches its first check.
        var reader = new CountingAgentFileReader(
            AgentFileReadResult.Success("a", AgentContentRevision.FromUtf8Text("a"), byteLength: 1));
        var mutator = new CountingAgentFileMutator();
        using var harness = new Phase22MediatedActionHarness(
            AgentBackendIds.NativeHarness,
            fileReader: reader,
            fileMutator: mutator);
        const string correlationKey = "p223-mismatch-initial";
        var key = AgentActionCorrelationKey.FromValue(correlationKey);
        var priorFingerprint = AgentActionRequestFingerprint.FromCanonicalText("prior-terminal");
        harness.CorrelationRegistry.RecordTerminalResult(
            key,
            priorFingerprint,
            new AgentActionResult(
                AgentActionId.New(),
                AgentActionAttemptId.New(),
                AgentActionResultKind.Succeeded,
                null,
                "prior terminal"));

        var result = await harness.Broker.RequestAsync(
            new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("b.txt")),
            correlationKey,
            CancellationToken.None);

        Assert.Equal(ContractAgentActionBroker.CorrelationMismatchSite.Initial, harness.Broker.TestLastCorrelationMismatchSite);
        AssertEarlyDenialAttribution(
            harness,
            result,
            AgentActionFailureKind.CorrelationKeyMismatch,
            reader,
            mutator);
    }

    [Fact]
    public async Task CorrelationMismatch_InFlightSite_ProducesExactlyOneCorrelatedEventAndAudit()
    {
        // Outer reject/terminal checks pass; a different fingerprint is registered
        // only after that, before TryWaitForInFlightReplay.
        var reader = new CountingAgentFileReader(
            AgentFileReadResult.Success("hold", AgentContentRevision.FromUtf8Text("hold"), byteLength: 4));
        var mutator = new CountingAgentFileMutator();
        using var harness = new Phase22MediatedActionHarness(
            AgentBackendIds.NativeHarness,
            fileReader: reader,
            fileMutator: mutator);
        const string correlationKey = "p223-mismatch-inflight";
        var key = AgentActionCorrelationKey.FromValue(correlationKey);
        var foreignFingerprint = AgentActionRequestFingerprint.FromCanonicalText("foreign-inflight");

        using var beforeWaitEntered = new ManualResetEventSlim(initialState: false);
        using var releaseBeforeWait = new ManualResetEventSlim(initialState: false);
        harness.Broker.TestBeforeOuterInFlightWait = () =>
        {
            beforeWaitEntered.Set();
            releaseBeforeWait.Wait();
        };

        var requestTask = Task.Run(async () =>
            await harness.Broker.RequestAsync(
                new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("subject.txt")),
                correlationKey,
                CancellationToken.None));

        Assert.True(beforeWaitEntered.Wait(TimeSpan.FromSeconds(5)));
        harness.CorrelationRegistry.BeginInFlightCorrelation(key, foreignFingerprint);
        releaseBeforeWait.Set();
        var result = await requestTask;

        Assert.Equal(ContractAgentActionBroker.CorrelationMismatchSite.InFlightWait, harness.Broker.TestLastCorrelationMismatchSite);
        AssertEarlyDenialAttribution(
            harness,
            result,
            AgentActionFailureKind.CorrelationKeyMismatch,
            reader,
            mutator);
    }

    [Fact]
    public async Task CorrelationMismatch_AdmissionGateSite_ProducesExactlyOneCorrelatedEventAndAudit()
    {
        // Outer checks pass with an empty registry; a different fingerprint is
        // recorded only after that, before the admission-gate re-check.
        var reader = new CountingAgentFileReader(
            AgentFileReadResult.Success("x", AgentContentRevision.FromUtf8Text("x"), byteLength: 1));
        var mutator = new CountingAgentFileMutator();
        using var harness = new Phase22MediatedActionHarness(
            AgentBackendIds.NativeHarness,
            fileReader: reader,
            fileMutator: mutator);
        const string correlationKey = "p223-mismatch-admission";
        var key = AgentActionCorrelationKey.FromValue(correlationKey);
        var foreignFingerprint = AgentActionRequestFingerprint.FromCanonicalText("foreign-admission");

        using var beforeGateEntered = new ManualResetEventSlim(initialState: false);
        using var releaseBeforeGate = new ManualResetEventSlim(initialState: false);
        harness.Broker.TestBeforeAdmissionGate = () =>
        {
            beforeGateEntered.Set();
            releaseBeforeGate.Wait();
        };

        var requestTask = Task.Run(async () =>
            await harness.Broker.RequestAsync(
                new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("gate-b.txt")),
                correlationKey,
                CancellationToken.None));

        Assert.True(beforeGateEntered.Wait(TimeSpan.FromSeconds(5)));
        harness.CorrelationRegistry.RecordTerminalResult(
            key,
            foreignFingerprint,
            new AgentActionResult(
                AgentActionId.New(),
                AgentActionAttemptId.New(),
                AgentActionResultKind.Succeeded,
                null,
                "foreign terminal"));
        releaseBeforeGate.Set();
        var result = await requestTask;

        Assert.Equal(ContractAgentActionBroker.CorrelationMismatchSite.AdmissionGate, harness.Broker.TestLastCorrelationMismatchSite);
        AssertEarlyDenialAttribution(
            harness,
            result,
            AgentActionFailureKind.CorrelationKeyMismatch,
            reader,
            mutator);
    }

    [Fact]
    public async Task CorrelationMismatch_ReservedInFlightSite_ProducesExactlyOneCorrelatedEventAndAudit()
    {
        // Initial and admission checks pass; run-slot reservation fails because
        // another request holds the slot without this correlation key; a different
        // fingerprint is registered only before the reserved-path wait.
        var reader = new CountingAgentFileReader(
            AgentFileReadResult.Success("slot", AgentContentRevision.FromUtf8Text("slot"), byteLength: 4));
        var mutator = new CountingAgentFileMutator();
        using var harness = new Phase22MediatedActionHarness(
            AgentBackendIds.NativeHarness,
            fileReader: reader,
            fileMutator: mutator);
        const string correlationKey = "p223-mismatch-reserved";
        var key = AgentActionCorrelationKey.FromValue(correlationKey);
        var foreignFingerprint = AgentActionRequestFingerprint.FromCanonicalText("foreign-reserved");

        using var slotHoldEntered = new ManualResetEventSlim(initialState: false);
        using var releaseSlotHold = new ManualResetEventSlim(initialState: false);
        harness.Broker.TestProcessingHold = () =>
        {
            slotHoldEntered.Set();
            releaseSlotHold.Wait();
        };

        // Holder occupies the run slot with no correlation key.
        var holderTask = Task.Run(async () =>
            await harness.Broker.RequestAsync(
                new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("holder.txt")),
                correlationKey: null,
                CancellationToken.None));
        Assert.True(slotHoldEntered.Wait(TimeSpan.FromSeconds(5)));

        using var beforeReservedWaitEntered = new ManualResetEventSlim(initialState: false);
        using var releaseBeforeReservedWait = new ManualResetEventSlim(initialState: false);
        harness.Broker.TestBeforeReservedInFlightWait = () =>
        {
            beforeReservedWaitEntered.Set();
            releaseBeforeReservedWait.Wait();
        };

        var subjectTask = Task.Run(async () =>
            await harness.Broker.RequestAsync(
                new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("subject.txt")),
                correlationKey,
                CancellationToken.None));

        Assert.True(beforeReservedWaitEntered.Wait(TimeSpan.FromSeconds(5)));
        harness.CorrelationRegistry.BeginInFlightCorrelation(key, foreignFingerprint);
        releaseBeforeReservedWait.Set();
        var result = await subjectTask;

        releaseSlotHold.Set();
        _ = await holderTask;

        Assert.Equal(
            ContractAgentActionBroker.CorrelationMismatchSite.ReservedInFlightWait,
            harness.Broker.TestLastCorrelationMismatchSite);
        AssertEarlyDenialAttribution(
            harness,
            result,
            AgentActionFailureKind.CorrelationKeyMismatch,
            reader,
            mutator,
            expectedReaderCount: 1); // holder read only
    }

    [Fact]
    public async Task RegistryRevocationAfterComposition_PreservesRequestActionAndAttemptIds()
    {
        var reader = new CountingAgentFileReader(
            AgentFileReadResult.Success("hold", AgentContentRevision.FromUtf8Text("hold"), byteLength: 4));
        var mutator = new CountingAgentFileMutator();
        using var harness = new Phase22MediatedActionHarness(
            AgentBackendIds.NativeHarness,
            fileReader: reader,
            fileMutator: mutator);
        const string correlationKey = "p223-revoke-after-compose";

        using var processingEntered = new ManualResetEventSlim(initialState: false);
        using var allowProcessingToComplete = new ManualResetEventSlim(initialState: false);
        harness.Broker.TestProcessingHold = () =>
        {
            processingEntered.Set();
            allowProcessingToComplete.Wait();
        };

        using var waitEntered = new ManualResetEventSlim(initialState: false);
        harness.CorrelationRegistry.TestOnInFlightWaitEntered = () => waitEntered.Set();

        var firstRequest = Task.Run(async () =>
            await harness.Broker.RequestAsync(
                new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("hold.txt")),
                correlationKey,
                CancellationToken.None));

        Assert.True(processingEntered.Wait(TimeSpan.FromSeconds(5)));

        var secondTask = Task.Run(async () =>
            await harness.Broker.RequestAsync(
                new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("hold.txt")),
                correlationKey,
                CancellationToken.None));

        // Explicit signal: second request entered the matching-fingerprint wait.
        Assert.True(waitEntered.Wait(TimeSpan.FromSeconds(5)));
        harness.Broker.Revoke();
        allowProcessingToComplete.Set();
        _ = await firstRequest;
        var second = await secondTask;

        Assert.Equal(AgentActionResultKind.Denied, second.ResultKind);
        Assert.Equal(AgentActionFailureKind.BrokerRevoked, second.FailureKind);
        // First completes after release and may perform one authorized read.
        AssertEarlyDenialAttribution(
            harness,
            second,
            AgentActionFailureKind.BrokerRevoked,
            reader,
            mutator,
            expectedReaderCount: 1);
    }

    /// <summary>
    /// Shared assertions for early-denial branch proof: one correlated event and
    /// audit, shared IDs, ZaideMediated evidence, exact workspace attribution,
    /// no mutation, and no residual run-slot occupancy.
    /// </summary>
    private static void AssertEarlyDenialAttribution(
        Phase22MediatedActionHarness harness,
        AgentActionResult result,
        AgentActionFailureKind expectedFailure,
        CountingAgentFileReader reader,
        CountingAgentFileMutator mutator,
        int expectedReaderCount = 0)
    {
        Assert.Equal(AgentActionResultKind.Denied, result.ResultKind);
        Assert.Equal(expectedFailure, result.FailureKind);

        var resultEvents = harness.CapturedEvents
            .Where(e =>
                e.Kind == AgentEventKind.ActionResultReported
                && e.Payload is AgentActionFactPayload p
                && p.ActionId == result.ActionId)
            .ToArray();
        Assert.Single(resultEvents);
        Assert.Equal(AgentActivityEvidenceLevel.ZaideMediated, resultEvents[0].EvidenceLevel);

        var fact = Assert.IsType<AgentActionFactPayload>(resultEvents[0].Payload);
        Assert.Equal(result.ActionId, fact.ActionId);
        Assert.Equal(result.AttemptId, fact.AttemptId);
        Assert.Equal(expectedFailure, fact.FailureKind);
        Assert.Equal(harness.Scope.Identity, fact.WorkspaceIdentity);
        Assert.Equal(harness.Scope.Generation, fact.WorkspaceGeneration);

        var audits = harness.AuditStore
            .GetRunSnapshot(harness.RunId, maxRecords: 64)
            .Where(r =>
                r.EventKind == AgentEventKind.ActionResultReported
                && r.ActionId == result.ActionId)
            .ToArray();
        Assert.Single(audits);
        Assert.Equal(result.AttemptId, audits[0].AttemptId);
        Assert.Equal(harness.Scope.Identity, audits[0].WorkspaceIdentity);
        Assert.Equal(harness.Scope.Generation, audits[0].WorkspaceGeneration);
        Assert.Equal(AgentActivityEvidenceLevel.ZaideMediated, audits[0].EvidenceLevel);

        Assert.Equal(expectedReaderCount, reader.ReadCount);
        Assert.Equal(0, mutator.ApplyCount);
        Assert.False(harness.RunSlot.HasActiveAction);
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
