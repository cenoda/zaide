using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Debugging.Application;
using Zaide.Features.Editor.Application;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Language.Application;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.SourceControl.Application;
using Zaide.Features.SourceControl.Contracts;
using Zaide.Features.SourceControl.Domain;
using Zaide.Tests.App.Composition;
using Zaide.Tests.Features.Agents;

namespace Zaide.Tests.Features.Agents.Application;

/// <summary>
/// Phase 18 M3 integration tests for run context manifest assembly and consumption boundary.
/// </summary>
public sealed class Phase18RunIntegrationTests
{
    private const string ContextAssemblyFailureReason = "IDE context assembly failed.";

    private static readonly DateTimeOffset FixedAssemblyTime =
        new(2026, 7, 26, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AgentBackendRequest_WithNullManifest_DoesNotThrow()
    {
        var request = new AgentBackendRequest(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            ConversationEntryId.New(),
            "test message",
            contextManifest: null);

        Assert.Null(request.ContextManifest);
    }

    [Fact]
    public void AgentBackendRequest_WithEmptyManifest_AcceptsManifest()
    {
        var manifest = new AgentContextManifest(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            AgentContextPolicyLevel.Off,
            Array.Empty<AgentContextItem>(),
            new AgentContextTokenBudget(AgentContextPolicyLevel.Off, 0, 0),
            Array.Empty<AgentContextTruncationDecision>(),
            Array.Empty<AgentContextExclusionDecision>(),
            FixedAssemblyTime);

        var request = new AgentBackendRequest(
            manifest.SessionId,
            manifest.RunId,
            manifest.ConversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            ConversationEntryId.New(),
            "test message",
            manifest);

        Assert.Same(manifest, request.ContextManifest);
    }

    [Fact]
    public void AgentBackendExecutionContext_ExposesManifestFromRequest()
    {
        var manifest = new AgentContextManifest(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            AgentContextPolicyLevel.Minimal,
            Array.Empty<AgentContextItem>(),
            new AgentContextTokenBudget(AgentContextPolicyLevel.Minimal, 100, 0),
            Array.Empty<AgentContextTruncationDecision>(),
            Array.Empty<AgentContextExclusionDecision>(),
            FixedAssemblyTime);

        var request = new AgentBackendRequest(
            manifest.SessionId,
            manifest.RunId,
            manifest.ConversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            ConversationEntryId.New(),
            "test message",
            manifest);

        var context = new AgentBackendExecutionContext(request, new UnavailableAgentActionBroker());

        Assert.Same(manifest, context.ContextManifest);
    }

    [Fact]
    public void AgentBackendExecutionContext_WithNullManifest_ReturnsNull()
    {
        var request = new AgentBackendRequest(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            ConversationEntryId.New(),
            "test message",
            contextManifest: null);

        var context = new AgentBackendExecutionContext(request, new UnavailableAgentActionBroker());

        Assert.Null(context.ContextManifest);
    }

    [Fact]
    public void Manifest_ItemsAreReadOnly()
    {
        var manifest = new AgentContextManifest(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            AgentContextPolicyLevel.Standard,
            new List<AgentContextItem> { CreateTestContextItem() },
            new AgentContextTokenBudget(AgentContextPolicyLevel.Standard, 1000, 500),
            Array.Empty<AgentContextTruncationDecision>(),
            Array.Empty<AgentContextExclusionDecision>(),
            FixedAssemblyTime);

        Assert.IsType<System.Collections.ObjectModel.ReadOnlyCollection<AgentContextItem>>(
            manifest.Items);
    }

    [Fact]
    public void Manifest_TruncationDecisionsAreReadOnly()
    {
        var manifest = new AgentContextManifest(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            AgentContextPolicyLevel.Standard,
            Array.Empty<AgentContextItem>(),
            new AgentContextTokenBudget(AgentContextPolicyLevel.Standard, 1000, 0),
            new List<AgentContextTruncationDecision>(),
            Array.Empty<AgentContextExclusionDecision>(),
            FixedAssemblyTime);

        Assert.IsType<System.Collections.ObjectModel.ReadOnlyCollection<AgentContextTruncationDecision>>(
            manifest.TruncationDecisions);
    }

    [Fact]
    public void Manifest_ExclusionDecisionsAreReadOnly()
    {
        var manifest = new AgentContextManifest(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            AgentContextPolicyLevel.Standard,
            Array.Empty<AgentContextItem>(),
            new AgentContextTokenBudget(AgentContextPolicyLevel.Standard, 1000, 0),
            Array.Empty<AgentContextTruncationDecision>(),
            new List<AgentContextExclusionDecision>(),
            FixedAssemblyTime);

        Assert.IsType<System.Collections.ObjectModel.ReadOnlyCollection<AgentContextExclusionDecision>>(
            manifest.ExclusionDecisions);
    }

    [Fact]
    public async Task SessionService_WithContextAssembly_AttachesManifestToRequest()
    {
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetCompletion("done");
        var eventStream = new AgentEventStream();
        var manifestBuilder = new AgentContextManifestBuilder();
        var snapshotSources = CreateDeterministicSnapshotSources();

        var sessionService = new AgentSessionService(
            new[] { backend },
            eventStream,
            contextManifestBuilder: manifestBuilder,
            contextSnapshotSources: snapshotSources);

        var conversationId = ConversationId.NewDirect();
        var messageEntryId = ConversationEntryId.New();

        var result = await sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            messageEntryId,
            "test message",
            CancellationToken.None);

        Assert.Equal(AgentRunStatus.Completed, result.Status);
        Assert.Equal(1, backend.ExecuteCallCount);

        var executionContext = backend.LastExecutionContext;
        Assert.NotNull(executionContext);
        var manifest = executionContext.ContextManifest;
        Assert.NotNull(manifest);
        Assert.Equal(conversationId, manifest.ConversationId);
        Assert.Equal(result.RunId, manifest.RunId);
        Assert.Equal(AgentContextPolicyLevel.Standard, manifest.PolicyLevelApplied);
        Assert.Contains(manifest.Items, item => item.SourceId == AgentContextSourceId.ProjectContext);
        Assert.IsType<System.Collections.ObjectModel.ReadOnlyCollection<AgentContextItem>>(manifest.Items);
    }

    [Fact]
    public async Task SessionService_WithContextAssembly_ManifestIdentityMatchesAdmittedRun()
    {
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetCompletion("done");
        var eventStream = new AgentEventStream();

        var sessionService = new AgentSessionService(
            new[] { backend },
            eventStream,
            contextManifestBuilder: new AgentContextManifestBuilder(),
            contextSnapshotSources: CreateDeterministicSnapshotSources());

        var conversationId = ConversationId.NewDirect();
        var result = await sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            ConversationEntryId.New(),
            "test message",
            CancellationToken.None);

        var manifest = backend.LastExecutionContext!.ContextManifest;
        Assert.NotNull(manifest);

        var sessionSnapshot = sessionService.TryGetSessionSnapshot(conversationId);
        Assert.NotNull(sessionSnapshot);
        Assert.Equal(sessionSnapshot.SessionId, manifest.SessionId);
        Assert.Equal(result.RunId, manifest.RunId);
        Assert.Equal(conversationId, manifest.ConversationId);
    }

    [Fact]
    public async Task SessionService_WithContextAssembly_AttachesManifestBeforeBackendExecution()
    {
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        backend.SetGatedCompletion(gate, "done");
        var eventStream = new AgentEventStream();

        var sessionService = new AgentSessionService(
            new[] { backend },
            eventStream,
            contextManifestBuilder: new AgentContextManifestBuilder(),
            contextSnapshotSources: CreateDeterministicSnapshotSources());

        var sendTask = sessionService.SendAsync(
            ConversationId.NewDirect(),
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            ConversationEntryId.New(),
            "test message",
            CancellationToken.None);

        await backend.ExecutionStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(backend.LastExecutionContext?.ContextManifest);

        gate.SetResult("done");
        var result = await sendTask;
        Assert.Equal(AgentRunStatus.Completed, result.Status);
    }

    [Fact]
    public async Task SessionService_WithoutContextAssembly_DoesNotAttachManifest()
    {
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetCompletion("done");
        var eventStream = new AgentEventStream();

        var sessionService = new AgentSessionService(
            new[] { backend },
            eventStream,
            contextManifestBuilder: null,
            contextSnapshotSources: null);

        var result = await sessionService.SendAsync(
            ConversationId.NewDirect(),
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            ConversationEntryId.New(),
            "test message",
            CancellationToken.None);

        Assert.Equal(AgentRunStatus.Completed, result.Status);
        Assert.Null(backend.LastExecutionContext?.ContextManifest);
    }

    [Fact]
    public async Task SessionService_WithOnlyManifestBuilder_DoesNotAttachManifest()
    {
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetCompletion("done");

        var sessionService = new AgentSessionService(
            new[] { backend },
            new AgentEventStream(),
            contextManifestBuilder: new AgentContextManifestBuilder(),
            contextSnapshotSources: null);

        await sessionService.SendAsync(
            ConversationId.NewDirect(),
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            ConversationEntryId.New(),
            "test message",
            CancellationToken.None);

        Assert.Null(backend.LastExecutionContext?.ContextManifest);
    }

    [Fact]
    public async Task SessionService_WithOnlySnapshotSources_DoesNotAttachManifest()
    {
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetCompletion("done");

        var sessionService = new AgentSessionService(
            new[] { backend },
            new AgentEventStream(),
            contextManifestBuilder: null,
            contextSnapshotSources: CreateDeterministicSnapshotSources());

        await sessionService.SendAsync(
            ConversationId.NewDirect(),
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            ConversationEntryId.New(),
            "test message",
            CancellationToken.None);

        Assert.Null(backend.LastExecutionContext?.ContextManifest);
    }

    [Fact]
    public async Task SessionService_RejectedRun_DoesNotInvokeBackendOrAttachManifest()
    {
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        backend.SetGatedCompletion(gate, "done");
        var eventStream = new AgentEventStream();

        var sessionService = new AgentSessionService(
            new[] { backend },
            eventStream,
            contextManifestBuilder: new AgentContextManifestBuilder(),
            contextSnapshotSources: CreateDeterministicSnapshotSources());

        var conversationId = ConversationId.NewDirect();
        var firstTask = sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            ConversationEntryId.New(),
            "first",
            CancellationToken.None);

        await backend.ExecutionStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(backend.LastExecutionContext?.ContextManifest);

        var rejected = await sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            ConversationEntryId.New(),
            "second",
            CancellationToken.None);

        Assert.Equal(AgentRunStatus.Rejected, rejected.Status);
        Assert.Equal(1, backend.ExecuteCallCount);

        gate.SetResult("done");
        var first = await firstTask;
        Assert.Equal(AgentRunStatus.Completed, first.Status);
    }

    [Fact]
    public async Task SessionService_BackendFailure_PreservesRunLifecycleAndManifestAttachment()
    {
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetFailure(AgentFailureKind.Execution, "execution failed");
        var eventStream = new AgentEventStream();

        var sessionService = new AgentSessionService(
            new[] { backend },
            eventStream,
            contextManifestBuilder: new AgentContextManifestBuilder(),
            contextSnapshotSources: CreateDeterministicSnapshotSources());

        var result = await sessionService.SendAsync(
            ConversationId.NewDirect(),
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            ConversationEntryId.New(),
            "test message",
            CancellationToken.None);

        Assert.Equal(AgentRunStatus.Failed, result.Status);
        Assert.NotNull(backend.LastExecutionContext?.ContextManifest);
    }

    [Fact]
    public async Task SessionService_Cancellation_PreservesRunLifecycleAndManifestAttachment()
    {
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        backend.SetGatedCompletion(gate, "done");
        var eventStream = new AgentEventStream();

        var sessionService = new AgentSessionService(
            new[] { backend },
            eventStream,
            contextManifestBuilder: new AgentContextManifestBuilder(),
            contextSnapshotSources: CreateDeterministicSnapshotSources());

        using var cts = new CancellationTokenSource();
        var conversationId = ConversationId.NewDirect();
        var sendTask = sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            ConversationEntryId.New(),
            "test message",
            cts.Token);

        await backend.ExecutionStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(backend.LastExecutionContext?.ContextManifest);
        cts.Cancel();

        var result = await sendTask;
        Assert.Equal(AgentRunStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task SessionService_AssemblyFailure_DoesNotAttachManifestAndEmitsSafeFailure()
    {
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetCompletion("done");
        var eventStream = new AgentEventStream();
        var failures = new List<AgentEvent>();
        using var subscription = eventStream.Events.Subscribe(failures.Add);

        var sessionService = new AgentSessionService(
            new[] { backend },
            eventStream,
            contextManifestBuilder: new AgentContextManifestBuilder(),
            contextSnapshotSources: new ThrowingAgentContextSnapshotSources());

        var result = await sessionService.SendAsync(
            ConversationId.NewDirect(),
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            ConversationEntryId.New(),
            "test message",
            CancellationToken.None);

        Assert.Equal(AgentRunStatus.Completed, result.Status);
        Assert.Null(backend.LastExecutionContext?.ContextManifest);

        var failure = failures.Single(e => e.Kind == AgentEventKind.FailureReported);
        var payload = Assert.IsType<AgentFailurePayload>(failure.Payload);
        Assert.Equal(AgentFailureKind.Indeterminate, payload.FailureKind);
        Assert.Equal(ContextAssemblyFailureReason, payload.Reason);
        Assert.DoesNotContain("secret", payload.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LegacyBackend_ExecuteAsync_DoesNotConsumeContextManifest()
    {
        var manifest = new AgentContextManifest(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            AgentContextPolicyLevel.Detailed,
            new List<AgentContextItem> { CreateTestContextItem() },
            new AgentContextTokenBudget(AgentContextPolicyLevel.Detailed, 2000, 1500),
            Array.Empty<AgentContextTruncationDecision>(),
            Array.Empty<AgentContextExclusionDecision>(),
            FixedAssemblyTime);

        var request = new AgentBackendRequest(
            manifest.SessionId,
            manifest.RunId,
            manifest.ConversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            ConversationEntryId.New(),
            "test message",
            manifest);

        var context = new AgentBackendExecutionContext(request, new UnavailableAgentActionBroker());

        var executeMethod = typeof(LegacyOpenAiCompatibleAgentBackend)
            .GetMethod(nameof(LegacyOpenAiCompatibleAgentBackend.ExecuteAsync));

        Assert.NotNull(executeMethod);

        var methodBody = executeMethod.ToString();
        Assert.DoesNotContain("ContextManifest", methodBody);
        Assert.DoesNotContain("AgentContextManifest", methodBody);
    }

    [Fact]
    public void ContextManifest_IdentityIsRunScoped()
    {
        var runId1 = ExecutionRunId.New();
        var runId2 = ExecutionRunId.New();
        var sessionId = AgentSessionId.New();
        var conversationId = ConversationId.NewDirect();

        var manifest1 = new AgentContextManifest(
            sessionId,
            runId1,
            conversationId,
            AgentContextPolicyLevel.Standard,
            Array.Empty<AgentContextItem>(),
            new AgentContextTokenBudget(AgentContextPolicyLevel.Standard, 1000, 0),
            Array.Empty<AgentContextTruncationDecision>(),
            Array.Empty<AgentContextExclusionDecision>(),
            FixedAssemblyTime);

        var manifest2 = new AgentContextManifest(
            sessionId,
            runId2,
            conversationId,
            AgentContextPolicyLevel.Standard,
            Array.Empty<AgentContextItem>(),
            new AgentContextTokenBudget(AgentContextPolicyLevel.Standard, 1000, 0),
            Array.Empty<AgentContextTruncationDecision>(),
            Array.Empty<AgentContextExclusionDecision>(),
            FixedAssemblyTime);

        Assert.NotEqual(manifest1.RunId, manifest2.RunId);
        Assert.NotSame(manifest1, manifest2);
    }

    [Fact]
    public void LiveSnapshotSources_ObservePublishedEditorAndSourceControlSnapshots()
    {
        using var provider = AgentsRegistrationModuleTests.BuildProductionProvider();
        var editorPublisher = (IEditorStateSnapshotPublisher)provider.GetRequiredService<IEditorStateSnapshotService>();
        var sourceControlPublisher = (ISourceControlSnapshotPublisher)provider.GetRequiredService<ISourceControlSnapshotService>();
        var snapshotSources = provider.GetRequiredService<IAgentContextSnapshotSources>();

        Assert.Null(snapshotSources.Editor.ActiveFilePath);
        Assert.Equal(SourceControlSnapshotAvailability.NoWorkspace, snapshotSources.SourceControl.Availability);

        Assert.True(editorPublisher.TryPublish(new EditorStateSnapshot(
            generation: 0,
            activeFilePath: "/workspace/Program.cs",
            activeFileContent: "class Program {}",
            openFilePaths: new[] { "/workspace/Program.cs" })));

        Assert.True(sourceControlPublisher.TryPublish(new SourceControlStatusSnapshot(
            generation: 0,
            availability: SourceControlSnapshotAvailability.Available,
            workspacePath: "/workspace",
            repositoryStatus: new RepositoryStatusSnapshot
            {
                CurrentBranchName = "main",
                Changes = new[] { new FileChange("Program.cs", GitChangeType.Modified) },
            })));

        Assert.Equal("/workspace/Program.cs", snapshotSources.Editor.ActiveFilePath);
        Assert.Equal(SourceControlSnapshotAvailability.Available, snapshotSources.SourceControl.Availability);
        Assert.Equal("main", snapshotSources.SourceControl.RepositoryStatus!.CurrentBranchName);
    }

    [Fact]
    public void ManifestBuilder_WithLivePublishedSnapshots_IncludesEditorAndSourceControlAtDetailedPolicy()
    {
        using var provider = AgentsRegistrationModuleTests.BuildProductionProvider();
        var editorPublisher = (IEditorStateSnapshotPublisher)provider.GetRequiredService<IEditorStateSnapshotService>();
        var sourceControlPublisher = (ISourceControlSnapshotPublisher)provider.GetRequiredService<ISourceControlSnapshotService>();
        var snapshotSources = provider.GetRequiredService<IAgentContextSnapshotSources>();
        var builder = provider.GetRequiredService<AgentContextManifestBuilder>();

        Assert.True(editorPublisher.TryPublish(new EditorStateSnapshot(
            generation: 0,
            activeFilePath: "/workspace/Program.cs",
            activeFileContent: "class Program {}",
            openFilePaths: new[] { "/workspace/Program.cs" },
            caretLine: 3,
            caretColumn: 5,
            selectionStart: 10,
            selectionLength: 4,
            selectionText: "Prog")));
        Assert.True(sourceControlPublisher.TryPublish(new SourceControlStatusSnapshot(
            generation: 0,
            availability: SourceControlSnapshotAvailability.Available,
            workspacePath: "/workspace",
            repositoryStatus: new RepositoryStatusSnapshot
            {
                CurrentBranchName = "main",
                Changes = new[] { new FileChange("Program.cs", GitChangeType.Modified) },
            })));

        var manifest = builder.Build(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            new AgentContextPolicy(AgentContextPolicyLevel.Detailed),
            snapshotSources,
            FixedAssemblyTime);

        Assert.Contains(
            manifest.Items,
            item => item.SourceId == AgentContextSourceId.ActiveFile);
        Assert.Contains(
            manifest.Items,
            item => item.SourceId == AgentContextSourceId.SourceControlSummary);
        Assert.Contains(
            manifest.Items,
            item => item.SourceId == AgentContextSourceId.EditorCaretSelection);
    }

    private static DeterministicAgentContextSnapshotSources CreateDeterministicSnapshotSources() =>
        new()
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

    private static AgentContextItem CreateTestContextItem() =>
        new(
            AgentContextSourceId.ActiveFile,
            "test content",
            "test://file",
            "test-fingerprint",
            AgentContextRedactionState.None,
            10,
            new AgentContextProvenance(
                "test-service",
                1,
                wasLiveSnapshot: true,
                redactionApplied: false,
                null),
            null);

    private sealed record DeterministicAgentContextSnapshotSources : IAgentContextSnapshotSources
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

    private sealed class ThrowingAgentContextSnapshotSources : IAgentContextSnapshotSources
    {
        public EditorStateSnapshot Editor => throw new InvalidOperationException("secret snapshot failure");

        public SourceControlStatusSnapshot SourceControl => throw new InvalidOperationException("secret snapshot failure");

        public LanguageDiagnosticsSnapshot LanguageDiagnostics => throw new InvalidOperationException("secret snapshot failure");

        public BuildDiagnosticsSnapshot BuildDiagnostics => throw new InvalidOperationException("secret snapshot failure");

        public ProjectWorkflowSnapshot Workflow => throw new InvalidOperationException("secret snapshot failure");

        public TestResultsSnapshot TestResults => throw new InvalidOperationException("secret snapshot failure");

        public DebugSessionSnapshot DebugSession => throw new InvalidOperationException("secret snapshot failure");

        public ProjectContext ProjectContext => throw new InvalidOperationException("secret snapshot failure");
    }
}
