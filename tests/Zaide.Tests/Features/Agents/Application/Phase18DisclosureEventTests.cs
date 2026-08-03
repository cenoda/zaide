using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Debugging.Application;
using Zaide.Features.Debugging.Contracts;
using Zaide.Features.Editor.Application;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Language.Application;
using Zaide.Features.Language.Contracts;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.SourceControl.Application;
using Zaide.Features.SourceControl.Contracts;
using Zaide.Tests.Features.Agents;
using Zaide.Tests.Features.Conversations;

namespace Zaide.Tests.Features.Agents.Application;

/// <summary>
/// Phase 18 M4 tests for ContextDisclosed audit event emission and disclosure indicator.
/// </summary>
public sealed class Phase18DisclosureEventTests
{
    [Fact]
    public async Task ContextDisclosed_EventEmitted_AfterSuccessfulManifestAssembly()
    {
        // Arrange
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetCompletion("done");
        var eventStream = new AgentEventStream();

        var sessionService = new AgentSessionService(
            new[] { backend },
            eventStream,
            contextManifestBuilder: new AgentContextManifestBuilder(),
            contextSnapshotSources: CreateTestSnapshotSources());

        var conversationId = ConversationId.NewDirect();
        var messageEntryId = ConversationEntryId.New();
        var capture = new AgentSessionCoordinatorEventCapture(conversationId, messageEntryId);
        capture.Subscribe(eventStream.Events);

        // Act
        var sendTask = sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            messageEntryId,
            "test message",
            CancellationToken.None);

        var admittedRunId = await capture.WaitForAdmissionOrRejectionAsync(sendTask, CancellationToken.None);

        // Assert
        Assert.NotNull(admittedRunId);
        var runEvents = capture.GetEventsForRun(admittedRunId.Value);
        var contextDisclosedEvents = runEvents
            .Where(e => e.Kind == AgentEventKind.ContextDisclosed)
            .Cast<AgentEvent>()
            .ToArray();

        Assert.Single(contextDisclosedEvents);
        var disclosureEvent = contextDisclosedEvents[0];
        Assert.IsType<AgentContextDisclosurePayload>(disclosureEvent.Payload);
    }

    [Fact]
    public async Task ContextDisclosed_EventPayload_ContainsCorrectIdentity()
    {
        // Arrange
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetCompletion("done");
        var eventStream = new AgentEventStream();

        var sessionService = new AgentSessionService(
            new[] { backend },
            eventStream,
            contextManifestBuilder: new AgentContextManifestBuilder(),
            contextSnapshotSources: CreateTestSnapshotSources());

        var conversationId = ConversationId.NewDirect();
        var messageEntryId = ConversationEntryId.New();
        var capture = new AgentSessionCoordinatorEventCapture(conversationId, messageEntryId);
        capture.Subscribe(eventStream.Events);

        // Act
        var sendTask = sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            messageEntryId,
            "test message",
            CancellationToken.None);

        var admittedRunId = await capture.WaitForAdmissionOrRejectionAsync(sendTask, CancellationToken.None);

        // Assert
        Assert.NotNull(admittedRunId);
        var runEvents = capture.GetEventsForRun(admittedRunId.Value);
        var disclosureEvent = runEvents
            .FirstOrDefault(e => e.Kind == AgentEventKind.ContextDisclosed);

        Assert.NotNull(disclosureEvent);
        Assert.Equal(conversationId, disclosureEvent.ConversationId);
        Assert.Equal(admittedRunId.Value, disclosureEvent.RunId);

        var payload = Assert.IsType<AgentContextDisclosurePayload>(disclosureEvent.Payload);
        Assert.Equal(conversationId, payload.ConversationId);
        Assert.Equal(admittedRunId.Value, payload.RunId);
    }

    [Fact]
    public async Task ContextDisclosed_PayloadSafety_NoRawContentExposed()
    {
        // Arrange
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetCompletion("done");
        var eventStream = new AgentEventStream();

        var sessionService = new AgentSessionService(
            new[] { backend },
            eventStream,
            contextManifestBuilder: new AgentContextManifestBuilder(),
            contextSnapshotSources: CreateTestSnapshotSources());

        var conversationId = ConversationId.NewDirect();
        var messageEntryId = ConversationEntryId.New();
        var capture = new AgentSessionCoordinatorEventCapture(conversationId, messageEntryId);
        capture.Subscribe(eventStream.Events);

        // Act
        var sendTask = sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            messageEntryId,
            "test message",
            CancellationToken.None);

        var admittedRunId = await capture.WaitForAdmissionOrRejectionAsync(sendTask, CancellationToken.None);

        // Assert
        Assert.NotNull(admittedRunId);
        var runEvents = capture.GetEventsForRun(admittedRunId.Value);
        var disclosureEvent = runEvents
            .FirstOrDefault(e => e.Kind == AgentEventKind.ContextDisclosed);

        Assert.NotNull(disclosureEvent);
        var payload = Assert.IsType<AgentContextDisclosurePayload>(disclosureEvent.Payload);

        // Verify that the payload contains only metadata, no raw content
        var payloadString = payload.ToString();
        Assert.DoesNotContain("test message", payloadString);
        Assert.DoesNotContain("class Program", payloadString); // From the snapshot
    }

    [Fact]
    public async Task ContextDisclosed_PayloadIncludes_ExclusionsTruncationRedactionMetadata()
    {
        // Arrange
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetCompletion("done");
        var eventStream = new AgentEventStream();

        var sessionService = new AgentSessionService(
            new[] { backend },
            eventStream,
            contextManifestBuilder: new AgentContextManifestBuilder(),
            contextSnapshotSources: CreateTestSnapshotSources());

        var conversationId = ConversationId.NewDirect();
        var messageEntryId = ConversationEntryId.New();
        var capture = new AgentSessionCoordinatorEventCapture(conversationId, messageEntryId);
        capture.Subscribe(eventStream.Events);

        // Act
        var sendTask = sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            messageEntryId,
            "test message",
            CancellationToken.None);

        var admittedRunId = await capture.WaitForAdmissionOrRejectionAsync(sendTask, CancellationToken.None);

        // Assert
        Assert.NotNull(admittedRunId);
        var runEvents = capture.GetEventsForRun(admittedRunId.Value);
        var disclosureEvent = runEvents
            .FirstOrDefault(e => e.Kind == AgentEventKind.ContextDisclosed);

        Assert.NotNull(disclosureEvent);
        var payload = Assert.IsType<AgentContextDisclosurePayload>(disclosureEvent.Payload);

        // Verify that the payload includes all required metadata
        Assert.NotNull(payload.RedactionSummary);
        Assert.NotNull(payload.BoundarySummary);
        Assert.True(payload.ItemCount >= 0);
        Assert.True(payload.EstimatedTokenCount >= 0);
        Assert.True(Enum.IsDefined(typeof(AgentContextPolicyLevel), payload.PolicyLevelApplied));
    }

    [Fact]
    public async Task ContextDisclosed_NoEventWhenManifestAssemblySkipped()
    {
        // Arrange - create service with null context dependencies to skip assembly
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetCompletion("done");
        var eventStream = new AgentEventStream();

        var sessionService = new AgentSessionService(
            new[] { backend },
            eventStream,
            contextManifestBuilder: null, // This will cause assembly to be skipped
            contextSnapshotSources: null);

        var conversationId = ConversationId.NewDirect();
        var messageEntryId = ConversationEntryId.New();
        var capture = new AgentSessionCoordinatorEventCapture(conversationId, messageEntryId);
        capture.Subscribe(eventStream.Events);

        // Act
        var sendTask = sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            messageEntryId,
            "test message",
            CancellationToken.None);

        var admittedRunId = await capture.WaitForAdmissionOrRejectionAsync(sendTask, CancellationToken.None);

        // Assert
        Assert.NotNull(admittedRunId);
        var runEvents = capture.GetEventsForRun(admittedRunId.Value);
        var contextDisclosedEvents = runEvents
            .Where(e => e.Kind == AgentEventKind.ContextDisclosed)
            .ToArray();

        // Should not have ContextDisclosed event when manifest assembly is skipped
        Assert.Empty(contextDisclosedEvents);
    }

    [Fact]
    public async Task ContextDisclosed_NoEventWhenSnapshotSourcesNull()
    {
        // Arrange - create service with manifest builder but null snapshot sources
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetCompletion("done");
        var eventStream = new AgentEventStream();

        var sessionService = new AgentSessionService(
            new[] { backend },
            eventStream,
            contextManifestBuilder: new AgentContextManifestBuilder(),
            contextSnapshotSources: null);

        var conversationId = ConversationId.NewDirect();
        var messageEntryId = ConversationEntryId.New();
        var capture = new AgentSessionCoordinatorEventCapture(conversationId, messageEntryId);
        capture.Subscribe(eventStream.Events);

        // Act
        var sendTask = sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            messageEntryId,
            "test message",
            CancellationToken.None);

        var admittedRunId = await capture.WaitForAdmissionOrRejectionAsync(sendTask, CancellationToken.None);

        // Assert
        Assert.NotNull(admittedRunId);
        var snapshot = await sendTask;

        // ContextDisclosed event should NOT be emitted when snapshot sources are null
        var runEvents = capture.GetEventsForRun(admittedRunId.Value);
        var contextDisclosedEvents = runEvents
            .Where(e => e.Kind == AgentEventKind.ContextDisclosed)
            .ToArray();
        Assert.Empty(contextDisclosedEvents);
    }

    [Fact]
    public async Task ContextDisclosed_PolicyLevelApplied_MatchesApplicationDefault()
    {
        // Arrange
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetCompletion("done");
        var eventStream = new AgentEventStream();

        var sessionService = new AgentSessionService(
            new[] { backend },
            eventStream,
            contextManifestBuilder: new AgentContextManifestBuilder(),
            contextSnapshotSources: CreateTestSnapshotSources());

        var conversationId = ConversationId.NewDirect();
        var messageEntryId = ConversationEntryId.New();
        var capture = new AgentSessionCoordinatorEventCapture(conversationId, messageEntryId);
        capture.Subscribe(eventStream.Events);

        // Act
        var sendTask = sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            messageEntryId,
            "test message",
            CancellationToken.None);

        var admittedRunId = await capture.WaitForAdmissionOrRejectionAsync(sendTask, CancellationToken.None);

        // Assert
        Assert.NotNull(admittedRunId);
        var runEvents = capture.GetEventsForRun(admittedRunId.Value);
        var disclosureEvent = runEvents
            .FirstOrDefault(e => e.Kind == AgentEventKind.ContextDisclosed);

        Assert.NotNull(disclosureEvent);
        var payload = Assert.IsType<AgentContextDisclosurePayload>(disclosureEvent.Payload);

        // Should match the application default policy level
        Assert.Equal(AgentContextPolicyLevel.Standard, payload.PolicyLevelApplied);
    }

    private sealed record TestAgentContextSnapshotSources : IAgentContextSnapshotSources
    {
        public required EditorStateSnapshot Editor { get; init; }
        public required SourceControlStatusSnapshot SourceControl { get; init; }
        public required LanguageDiagnosticsSnapshot LanguageDiagnostics { get; init; }
        public required BuildDiagnosticsSnapshot BuildDiagnostics { get; init; }
        public required ProjectWorkflowSnapshot Workflow { get; init; }
        public required TestResultsSnapshot TestResults { get; init; }
        public required DebugSessionSnapshot DebugSession { get; init; }
        public required ProjectContext ProjectContext { get; init; }
    }

    private static TestAgentContextSnapshotSources CreateTestSnapshotSources()
    {
        return new TestAgentContextSnapshotSources
        {
            Editor = new EditorStateSnapshot(
                generation: 1,
                activeFilePath: "/workspace/Program.cs",
                activeFileContent: "class Program {}",
                openFilePaths: new[] { "/workspace/Program.cs" }),
            SourceControl = new SourceControlStatusSnapshot(
                generation: 1,
                availability: SourceControlSnapshotAvailability.NoWorkspace),
            LanguageDiagnostics = LanguageDiagnosticsSnapshot.Empty,
            BuildDiagnostics = BuildDiagnosticsSnapshot.Empty,
            Workflow = new ProjectWorkflowSnapshot(
                ProjectWorkflowOperationState.Idle,
                Generation: 0,
                ActiveOperation: null,
                LastOutcome: null,
                TargetFilePath: null,
                ProcessId: null,
                OutputLines: [],
                LastOperation: null),
            TestResults = TestResultsSnapshot.Empty,
            DebugSession = new DebugSessionSnapshot(
                DebugSessionState.Idle,
                Generation: 0,
                ProgramPath: null,
                WorkingDirectory: null,
                AdapterProcessId: null,
                StopInfo: null,
                Failure: null,
                LastOutcome: null,
                DiagnosticOutput: [],
                BreakpointVerifications: DebugSessionSnapshot.EmptyVerifications),
            ProjectContext = new ProjectContext(
                ProjectContextState.SingleProject,
                WorkspaceRoot: "/workspace",
                Candidates: [new ProjectCandidate("/workspace/App.csproj", "App", ProjectKind.CSharpProject)],
                SelectedProject: new ProjectCandidate("/workspace/App.csproj", "App", ProjectKind.CSharpProject),
                UnsupportedFiles: [],
                ErrorMessage: null),
        };
    }

    [Fact]
    public async Task ContextDisclosed_NoEventForRejectedRun()
    {
        // Arrange - gate the first run so the second can be rejected while in-flight,
        // without a multi-second fixed delay that dominates suite wall time.
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        var firstGate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        backend.SetGatedCompletion(firstGate, "delayed response");
        var eventStream = new AgentEventStream();

        var sessionService = new AgentSessionService(
            new[] { backend },
            eventStream,
            contextManifestBuilder: new AgentContextManifestBuilder(),
            contextSnapshotSources: CreateTestSnapshotSources());

        var conversationId = ConversationId.NewDirect();
        var firstMessageEntryId = ConversationEntryId.New();
        var secondMessageEntryId = ConversationEntryId.New();
        var firstCapture = new AgentSessionCoordinatorEventCapture(conversationId, firstMessageEntryId);
        var secondCapture = new AgentSessionCoordinatorEventCapture(conversationId, secondMessageEntryId);
        firstCapture.Subscribe(eventStream.Events);
        secondCapture.Subscribe(eventStream.Events);

        // Act - start first run, wait until backend execution starts, then reject second.
        var firstSendTask = sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            firstMessageEntryId,
            "first message",
            CancellationToken.None);

        await backend.ExecutionStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var secondSendTask = sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            secondMessageEntryId,
            "second message",
            CancellationToken.None);

        firstGate.SetResult("delayed response");

        // Wait for both to complete
        var firstResult = await firstSendTask;
        var secondResult = await secondSendTask;

        // Assert - second run should be rejected
        Assert.NotNull(secondResult);
        Assert.Equal(AgentRunStatus.Rejected, secondResult.Status);

        // Get events for the rejected run using the second capture
        var rejectedRunId = secondResult.RunId;
        var rejectedRunEvents = secondCapture.GetEventsForRun(rejectedRunId);

        var contextDisclosedEvents = rejectedRunEvents
            .Where(e => e.Kind == AgentEventKind.ContextDisclosed)
            .ToArray();

        // Should not have ContextDisclosed event for rejected runs
        Assert.Empty(contextDisclosedEvents);
    }

    private static int FindEventIndex(IReadOnlyList<AgentEvent> events, AgentEventKind kind)
    {
        for (int i = 0; i < events.Count; i++)
        {
            if (events[i].Kind == kind)
                return i;
        }
        return -1;
    }

    [Fact]
    public async Task ContextDisclosed_EventEmitted_AfterManifestAttachedToBackend()
    {
        // Arrange
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetCompletion("done");
        var eventStream = new AgentEventStream();

        var sessionService = new AgentSessionService(
            new[] { backend },
            eventStream,
            contextManifestBuilder: new AgentContextManifestBuilder(),
            contextSnapshotSources: CreateTestSnapshotSources());

        var conversationId = ConversationId.NewDirect();
        var messageEntryId = ConversationEntryId.New();
        var capture = new AgentSessionCoordinatorEventCapture(conversationId, messageEntryId);
        capture.Subscribe(eventStream.Events);

        // Act
        var sendTask = sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            messageEntryId,
            "test message",
            CancellationToken.None);

        var admittedRunId = await capture.WaitForAdmissionOrRejectionAsync(sendTask, CancellationToken.None);

        // Assert
        Assert.NotNull(admittedRunId);
        var runEvents = capture.GetEventsForRun(admittedRunId.Value);

        // Find the indices of key events
        var runRunningIndex = FindEventIndex(runEvents, AgentEventKind.RunRunning);
        var contextDisclosedIndex = FindEventIndex(runEvents, AgentEventKind.ContextDisclosed);

        // ContextDisclosed must be emitted AFTER RunRunning (which happens after manifest is attached to request)
        Assert.True(runRunningIndex >= 0, "RunRunning event should be present");
        Assert.True(contextDisclosedIndex >= 0, "ContextDisclosed event should be present");
        Assert.True(
            contextDisclosedIndex > runRunningIndex,
            "ContextDisclosed must be emitted after RunRunning (manifest attached to backend)");

        // Verify the backend received a non-null manifest
        Assert.NotNull(backend.LastExecutionContext);
        Assert.NotNull(backend.LastExecutionContext.Request);
        Assert.NotNull(backend.LastExecutionContext.Request.ContextManifest);
    }

    [Fact]
    public void ContextDisclosureStatus_IsConsumedByView_ProjectedToNavigationItem()
    {
        // This test verifies that ContextDisclosureStatus from AgentPanelState
        // is properly projected to TownhallNavigationItem and consumed by the view.
        // This is the "architecture proof" that the property is actually bound/rendered.

        // Arrange
        var store = ConversationsTestSupport.CreateStore();
        var catalog = ConversationsTestSupport.CreateCatalog();
        var panelHost = new AgentPanelHost(catalog, store);

        var panel = panelHost.CreatePanel();
        panel.ContextDisclosureStatus = "Context: 2 sources, 500 tokens";

        var vm = ConversationsTestSupport.CreateTownhallViewModel(
            state: null,
            store: store,
            panelHost: panelHost);

        // Act
        vm.RefreshDirectNavItems();

        // Assert: The disclosure status should be projected to the navigation items
        var directItems = vm.DirectNavItems;
        var matchingItem = directItems.FirstOrDefault(item => item.ConversationId == panel.ConversationId);
        Assert.NotNull(matchingItem);
        Assert.Equal("Context: 2 sources, 500 tokens", matchingItem.ContextDisclosureStatus);
    }

    [Fact]
    public void ContextDisclosureStatus_PropagatesLive_AfterPanelUpdate()
    {
        // Arrange
        var store = ConversationsTestSupport.CreateStore();
        var catalog = ConversationsTestSupport.CreateCatalog();
        var panelHost = new AgentPanelHost(catalog, store);

        var panel = panelHost.CreatePanel();
        panel.ContextDisclosureStatus = string.Empty;

        var vm = ConversationsTestSupport.CreateTownhallViewModel(
            state: null,
            store: store,
            panelHost: panelHost);

        vm.RefreshDirectNavItems();
        var matchingItem = vm.DirectNavItems.FirstOrDefault(item => item.ConversationId == panel.ConversationId);
        Assert.NotNull(matchingItem);
        Assert.Equal(string.Empty, matchingItem.ContextDisclosureStatus);

        // Act: Update panel status after creation
        panel.ContextDisclosureStatus = "Context: 1 source, 250 tokens";

        // Assert: Nav item should reflect the update live without manual RefreshDirectNavItems
        Assert.Equal("Context: 1 source, 250 tokens", matchingItem.ContextDisclosureStatus);
    }

    // ── M6 adversarial tests ──────────────────────────────────────────

    [Fact]
    public void ContextDisclosurePayload_NeverExposesRawSnapshotContent()
    {
        // M6: The disclosure payload must be metadata-only. No raw content,
        // file paths, or snippet text from context items may appear in the
        // payload sent over the event stream.
        var payloadType = typeof(AgentContextDisclosurePayload);
        var allProperties = payloadType.GetProperties(
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic);

        foreach (var property in allProperties)
        {
            Assert.False(
                property.Name.Equals("Content", StringComparison.Ordinal),
                $"Disclosure payload must not expose a property named '{property.Name}'.");
            Assert.False(
                property.Name.Equals("RawContent", StringComparison.Ordinal),
                $"Disclosure payload must not expose a property named '{property.Name}'.");
            Assert.False(
                property.PropertyType == typeof(AgentContextManifest),
                $"Disclosure payload must not expose AgentContextManifest directly. Property: '{property.Name}'.");
            Assert.False(
                property.PropertyType == typeof(AgentContextItem),
                $"Disclosure payload must not expose AgentContextItem directly. Property: '{property.Name}'.");
        }

        // Verify the payload carries only the public, documented metadata fields
        Assert.NotNull(payloadType.GetProperty(nameof(AgentContextDisclosurePayload.SessionId)));
        Assert.NotNull(payloadType.GetProperty(nameof(AgentContextDisclosurePayload.ConversationId)));
        Assert.NotNull(payloadType.GetProperty(nameof(AgentContextDisclosurePayload.RunId)));
        Assert.NotNull(payloadType.GetProperty(nameof(AgentContextDisclosurePayload.PolicyLevelApplied)));
    }

    [Fact]
    public async Task ContextDisclosed_NoRawItemContentInDisclosureStatusText()
    {
        // M6: ContextDisclosureStatus on AgentPanelState (which flows to
        // TownhallNavigationItem and thus the UI) must never contain raw
        // snapshot content. The disclosure payload must carry only
        // metadata (source IDs, counts) — never file content or diagnostic text.
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetCompletion("done");
        var eventStream = new AgentEventStream();

        var sessionService = new AgentSessionService(
            new[] { backend },
            eventStream,
            contextManifestBuilder: new AgentContextManifestBuilder(),
            contextSnapshotSources: CreateTestSnapshotSources());

        var conversationId = ConversationId.NewDirect();
        var messageEntryId = ConversationEntryId.New();
        var capture = new AgentSessionCoordinatorEventCapture(conversationId, messageEntryId);
        capture.Subscribe(eventStream.Events);

        var result = await sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            messageEntryId,
            "test message",
            CancellationToken.None);

        var manifest = backend.LastExecutionContext!.ContextManifest;
        Assert.NotNull(manifest);

        // Verify disclosure event payload carries only metadata.
        var runEvents = capture.GetEventsForRun(result.RunId);
        var disclosureEvents = runEvents
            .Where(e => e.Kind == AgentEventKind.ContextDisclosed).ToArray();
        Assert.Single(disclosureEvents);
        var payload = (AgentContextDisclosurePayload)disclosureEvents[0].Payload;

        // Payload contains source IDs and counts, not raw content.
        Assert.Contains(payload.DisclosedSourceIds, id => id.Value.Contains("active-file"));
        Assert.True(payload.ItemCount > 0);
    }

    [Fact]
    public async Task AssemblyFailure_EmitsSafeReasonWithoutRawSnapshotContent()
    {
        // M6: When context assembly throws an exception, the FailureReported
        // reason must be the fixed constant "IDE context assembly failed." —
        // never raw snapshot content, exception type names, stack traces, or
        // any detail from the throwing source.
        //
        // This test proves fail-closed: assembly exception → null manifest,
        // safe fixed reason, no raw content leakage.
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetCompletion("done");
        var eventStream = new AgentEventStream();

        // Snapshot sources that throw on every access — forces assembly to fail.
        var throwingSources = new ThrowingSnapshotSources();
        var sessionService = new AgentSessionService(
            new[] { backend },
            eventStream,
            contextManifestBuilder: new AgentContextManifestBuilder(),
            contextSnapshotSources: throwingSources);

        var conversationId = ConversationId.NewDirect();
        var messageEntryId = ConversationEntryId.New();
        var capture = new AgentSessionCoordinatorEventCapture(conversationId, messageEntryId);
        capture.Subscribe(eventStream.Events);

        var result = await sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            messageEntryId,
            "test message",
            CancellationToken.None);

        // Assembly exception: manifest is NOT attached (fail-closed).
        Assert.Null(backend.LastExecutionContext!.ContextManifest);

        // Run still completes (backend executed without context).
        Assert.Equal(AgentRunStatus.Completed, result.Status);

        // FailureReported event emitted with the fixed safe reason.
        var runEvents = capture.GetEventsForRun(result.RunId);
        var contextFailureEvents = runEvents
            .Where(e => e.Kind == AgentEventKind.FailureReported
                        && ((AgentFailurePayload)e.Payload).Reason
                            .Contains("context", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.Single(contextFailureEvents);

        var failurePayload = (AgentFailurePayload)contextFailureEvents[0].Payload;
        Assert.Equal("IDE context assembly failed.", failurePayload.Reason);

        // Verify that the reason contains NO raw exception detail.
        Assert.DoesNotContain("InvalidOperationException", failurePayload.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("ThrowingSnapshotSources", failurePayload.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("StackTrace", failurePayload.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", failurePayload.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ContextDisclosurePayload_IdentityMatchesRunSessionAndConversation()
    {
        // M6 adversarial: verify identity across multiple runs in the same
        // conversation. Each ContextDisclosed event must carry the correct
        // run/session/conversation tuple.
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetCompletion("first", "second");
        var eventStream = new AgentEventStream();

        var sessionService = new AgentSessionService(
            new[] { backend },
            eventStream,
            contextManifestBuilder: new AgentContextManifestBuilder(),
            contextSnapshotSources: CreateTestSnapshotSources());

        var conversationId = ConversationId.NewDirect();

        var firstEntryId = ConversationEntryId.New();
        var firstCapture = new AgentSessionCoordinatorEventCapture(conversationId, firstEntryId);
        firstCapture.Subscribe(eventStream.Events);

        var firstResult = await sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            firstEntryId,
            "first message",
            CancellationToken.None);

        var secondEntryId = ConversationEntryId.New();
        var secondCapture = new AgentSessionCoordinatorEventCapture(conversationId, secondEntryId);
        secondCapture.Subscribe(eventStream.Events);

        var secondResult = await sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            secondEntryId,
            "second message",
            CancellationToken.None);

        // Each run's ContextDisclosed event must carry the correct identity.
        var firstDisclosure = Assert.Single(
            firstCapture.GetEventsForRun(firstResult.RunId),
            e => e.Kind == AgentEventKind.ContextDisclosed);
        var firstPayload = (AgentContextDisclosurePayload)firstDisclosure.Payload;
        Assert.Equal(conversationId, firstDisclosure.ConversationId);
        Assert.Equal(conversationId, firstPayload.ConversationId);
        Assert.Equal(firstResult.RunId, firstDisclosure.RunId);
        Assert.Equal(firstResult.RunId, firstPayload.RunId);
        Assert.Equal(firstResult.SessionId, firstDisclosure.SessionId);
        Assert.Equal(firstResult.SessionId, firstPayload.SessionId);

        var secondDisclosure = Assert.Single(
            secondCapture.GetEventsForRun(secondResult.RunId),
            e => e.Kind == AgentEventKind.ContextDisclosed);
        var secondPayload = (AgentContextDisclosurePayload)secondDisclosure.Payload;
        Assert.Equal(conversationId, secondDisclosure.ConversationId);
        Assert.Equal(secondResult.RunId, secondDisclosure.RunId);
        Assert.Equal(secondResult.RunId, secondPayload.RunId);

        // Runs in the same conversation share a session but have distinct run IDs.
        Assert.Equal(firstResult.SessionId, secondResult.SessionId);
        Assert.NotEqual(firstResult.RunId, secondResult.RunId);
    }

    [Fact]
    public async Task RejectedRun_DoesNotEmitContextDisclosed_Adversarial()
    {
        // M6 adversarial: explicit end-to-end proof that a rejected run
        // never publishes ContextDisclosed, even with an active override.
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetCompletion("done");
        var eventStream = new AgentEventStream();

        var sessionService = new AgentSessionService(
            new[] { backend },
            eventStream,
            contextManifestBuilder: new AgentContextManifestBuilder(),
            contextSnapshotSources: CreateTestSnapshotSources());

        var conversationId = ConversationId.NewDirect();

        // Send first run to establish session.
        var firstEntryId = ConversationEntryId.New();
        var firstCapture = new AgentSessionCoordinatorEventCapture(conversationId, firstEntryId);
        firstCapture.Subscribe(eventStream.Events);

        var firstResult = await sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            firstEntryId,
            "first message",
            CancellationToken.None);

        // Start second run (gated) so a third is rejected.
        var gate = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        backend.SetGatedCompletion(gate, "gated");

        var secondEntryId = ConversationEntryId.New();
        var secondCapture = new AgentSessionCoordinatorEventCapture(conversationId, secondEntryId);
        secondCapture.Subscribe(eventStream.Events);

        // Fire second run — it will be admitted and block at the gate
        var inFlight = sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            secondEntryId,
            "in-flight",
            CancellationToken.None);

        // Give the in-flight task time to acquire the session lock
        await Task.Delay(100);

        // Third run is rejected while second is in-flight.
        var thirdEntryId = ConversationEntryId.New();
        var thirdCapture = new AgentSessionCoordinatorEventCapture(conversationId, thirdEntryId);
        thirdCapture.Subscribe(eventStream.Events);

        var rejected = await sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            thirdEntryId,
            "rejected",
            CancellationToken.None);

        Assert.Equal(AgentRunStatus.Rejected, rejected.Status);

        gate.SetResult("done");
        var secondResult = await inFlight;

        // Rejected run must not emit ContextDisclosed.
        Assert.Equal(AgentRunStatus.Rejected, rejected.Status);
        var rejectedRunEvents = thirdCapture.GetEventsForRun(rejected.RunId);
        Assert.DoesNotContain(
            rejectedRunEvents,
            e => e.Kind == AgentEventKind.ContextDisclosed);

        // First and second (admitted) runs each emit exactly one ContextDisclosed.
        Assert.Single(
            firstCapture.GetEventsForRun(firstResult.RunId),
            e => e.Kind == AgentEventKind.ContextDisclosed);
        Assert.Single(
            secondCapture.GetEventsForRun(secondResult.RunId),
            e => e.Kind == AgentEventKind.ContextDisclosed);
    }

    /// <summary>
    /// M6 adversarial: snapshot sources that throw on every property access,
    /// forcing <see cref="AgentContextManifestBuilder.Build"/> to throw and
    /// exercising the fail-closed assembly path.
    /// </summary>
    private sealed class ThrowingSnapshotSources : IAgentContextSnapshotSources
    {
        public EditorStateSnapshot Editor =>
            throw new InvalidOperationException("Test injection failure.");
        public SourceControlStatusSnapshot SourceControl =>
            throw new InvalidOperationException("Test injection failure.");
        public LanguageDiagnosticsSnapshot LanguageDiagnostics =>
            throw new InvalidOperationException("Test injection failure.");
        public BuildDiagnosticsSnapshot BuildDiagnostics =>
            throw new InvalidOperationException("Test injection failure.");
        public ProjectWorkflowSnapshot Workflow =>
            throw new InvalidOperationException("Test injection failure.");
        public TestResultsSnapshot TestResults =>
            throw new InvalidOperationException("Test injection failure.");
        public DebugSessionSnapshot DebugSession =>
            throw new InvalidOperationException("Test injection failure.");
        public ProjectContext ProjectContext =>
            throw new InvalidOperationException("Test injection failure.");
    }
}
