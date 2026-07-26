using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
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
        // Arrange - create a backend with delayed completion to allow rejection scenario
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetDelayedCompletion(TimeSpan.FromSeconds(1), "delayed response");
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

        // Act - start first run, then immediately try second run which should be rejected
        var firstSendTask = sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            firstMessageEntryId,
            "first message",
            CancellationToken.None);

        // Wait a bit for the first run to be admitted
        await Task.Delay(50);

        var secondSendTask = sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            secondMessageEntryId,
            "second message",
            CancellationToken.None);

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
}