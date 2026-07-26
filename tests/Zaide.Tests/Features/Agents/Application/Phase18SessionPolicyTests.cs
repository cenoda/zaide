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
using Zaide.Features.Editor.Application;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Language.Application;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.SourceControl.Application;
using Zaide.Features.SourceControl.Contracts;
using Zaide.Features.SourceControl.Domain;

namespace Zaide.Tests.Features.Agents.Application;

/// <summary>
/// Phase 18 M5 tests for session policy override precedence and session boundary behavior.
/// </summary>
public sealed class Phase18SessionPolicyTests
{
    [Fact]
    public void GetPolicyState_WithoutOverride_ReportsApplicationDefault()
    {
        var sessionService = CreateSessionService();
        var conversationId = ConversationId.NewDirect();

        var state = sessionService.GetPolicyState(conversationId);

        Assert.Equal(AgentSessionContextPolicyLevel.Standard, state.ApplicationDefaultLevel);
        Assert.Equal(AgentSessionContextPolicyLevel.Standard, state.EffectiveLevel);
        Assert.False(state.IsOverrideActive);
        Assert.Equal(
            AgentContextSessionPolicyState.FormatApplicationDefaultCaption(
                AgentSessionContextPolicyLevel.Standard),
            state.StatusCaption);
    }

    [Theory]
    [InlineData(AgentSessionContextPolicyLevel.Off)]
    [InlineData(AgentSessionContextPolicyLevel.Minimal)]
    [InlineData(AgentSessionContextPolicyLevel.Standard)]
    [InlineData(AgentSessionContextPolicyLevel.Detailed)]
    public void TrySetSessionOverride_PrecedesApplicationDefault(AgentSessionContextPolicyLevel level)
    {
        var sessionService = CreateSessionService();
        var conversationId = ConversationId.NewDirect();

        Assert.True(sessionService.TrySetSessionOverride(conversationId, level));

        var state = sessionService.GetPolicyState(conversationId);
        Assert.Equal(AgentSessionContextPolicyLevel.Standard, state.ApplicationDefaultLevel);
        Assert.Equal(level, state.EffectiveLevel);
        Assert.True(state.IsOverrideActive);
        Assert.Equal(AgentContextSessionPolicyState.FormatOverrideCaption(level), state.StatusCaption);
    }

    [Fact]
    public void ClearSessionOverride_RestoresApplicationDefault()
    {
        var sessionService = CreateSessionService();
        var conversationId = ConversationId.NewDirect();

        sessionService.TrySetSessionOverride(conversationId, AgentSessionContextPolicyLevel.Minimal);
        Assert.True(sessionService.ClearSessionOverride(conversationId));

        var state = sessionService.GetPolicyState(conversationId);
        Assert.False(state.IsOverrideActive);
        Assert.Equal(AgentSessionContextPolicyLevel.Standard, state.EffectiveLevel);
    }

    [Fact]
    public void SessionPolicyOverrides_AreIsolatedPerConversation()
    {
        var sessionService = CreateSessionService();
        var conversationA = ConversationId.NewDirect();
        var conversationB = ConversationId.NewDirect();

        sessionService.TrySetSessionOverride(conversationA, AgentSessionContextPolicyLevel.Detailed);

        Assert.Equal(
            AgentSessionContextPolicyLevel.Detailed,
            sessionService.GetPolicyState(conversationA).EffectiveLevel);
        Assert.False(sessionService.GetPolicyState(conversationB).IsOverrideActive);
        Assert.Equal(
            AgentSessionContextPolicyLevel.Standard,
            sessionService.GetPolicyState(conversationB).EffectiveLevel);
    }

    [Theory]
    [InlineData(AgentSessionContextPolicyLevel.Off)]
    [InlineData(AgentSessionContextPolicyLevel.Minimal)]
    [InlineData(AgentSessionContextPolicyLevel.Standard)]
    [InlineData(AgentSessionContextPolicyLevel.Detailed)]
    public async Task SubsequentRun_UsesUpdatedSessionOverride(AgentSessionContextPolicyLevel overrideLevel)
    {
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetCompletion("done", "done");
        var sessionService = CreateSessionService(backend);

        var conversationId = ConversationId.NewDirect();
        sessionService.TrySetSessionOverride(conversationId, overrideLevel);

        var first = await SendAsync(sessionService, backend, conversationId);
        var second = await SendAsync(sessionService, backend, conversationId);

        Assert.Equal(AgentRunStatus.Completed, first.Status);
        Assert.Equal(AgentRunStatus.Completed, second.Status);

        var manifest = backend.LastExecutionContext!.ContextManifest;
        Assert.NotNull(manifest);
        Assert.Equal(MapToDomain(overrideLevel), manifest.PolicyLevelApplied);

        if (overrideLevel == AgentSessionContextPolicyLevel.Off)
        {
            Assert.Empty(manifest.Items);
        }
        else
        {
            Assert.NotEmpty(manifest.Items);
        }
    }

    [Fact]
    public async Task SubsequentRun_DetailedOverride_IncludesMoreSourcesThanMinimal()
    {
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetCompletion("minimal", "detailed");
        var sessionService = CreateSessionService(backend);
        var conversationId = ConversationId.NewDirect();

        sessionService.TrySetSessionOverride(conversationId, AgentSessionContextPolicyLevel.Minimal);
        await SendAsync(sessionService, backend, conversationId);
        var minimalCount = backend.LastExecutionContext!.ContextManifest!.Items.Count;

        sessionService.TrySetSessionOverride(conversationId, AgentSessionContextPolicyLevel.Detailed);
        await SendAsync(sessionService, backend, conversationId);
        var detailedCount = backend.LastExecutionContext.ContextManifest!.Items.Count;

        Assert.True(detailedCount > minimalCount);
    }

    [Fact]
    public async Task ExistingRunManifest_IsImmutable_WhenOverrideChangesBeforeSecondRun()
    {
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetCompletion("first", "second");
        var sessionService = CreateSessionService(backend);
        var conversationId = ConversationId.NewDirect();

        var firstResult = await SendAsync(sessionService, backend, conversationId);
        var firstManifest = backend.LastExecutionContext!.ContextManifest;
        Assert.NotNull(firstManifest);
        Assert.Equal(AgentContextPolicyLevel.Standard, firstManifest.PolicyLevelApplied);

        sessionService.TrySetSessionOverride(conversationId, AgentSessionContextPolicyLevel.Minimal);

        var secondResult = await SendAsync(sessionService, backend, conversationId);
        var secondManifest = backend.LastExecutionContext.ContextManifest;

        Assert.Equal(AgentRunStatus.Completed, firstResult.Status);
        Assert.Equal(AgentRunStatus.Completed, secondResult.Status);
        Assert.Equal(AgentContextPolicyLevel.Standard, firstManifest.PolicyLevelApplied);
        Assert.Equal(AgentContextPolicyLevel.Minimal, secondManifest!.PolicyLevelApplied);
        Assert.NotEqual(firstManifest.Items.Count, secondManifest.Items.Count);
    }

    [Fact]
    public async Task RejectedRun_DoesNotInvokeBackendOrAssembleManifest()
    {
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetCompletion("done");
        var sessionService = CreateSessionService(backend);
        var conversationId = ConversationId.NewDirect();

        sessionService.TrySetSessionOverride(conversationId, AgentSessionContextPolicyLevel.Detailed);

        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        backend.SetGatedCompletion(gate, "done");
        var inFlight = sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            ConversationEntryId.New(),
            "first",
            CancellationToken.None);

        await backend.ExecutionStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

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
        await inFlight;

        Assert.Equal(
            AgentSessionContextPolicyLevel.Detailed,
            sessionService.GetPolicyState(conversationId).EffectiveLevel);
    }

    [Fact]
    public async Task ConcurrentPolicyUpdate_DoesNotMutateApplicationDefault()
    {
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetCompletion(
            "one",
            "two",
            "three",
            "four",
            "five",
            "six",
            "seven",
            "eight");
        var sessionService = CreateSessionService(backend);
        var conversationId = ConversationId.NewDirect();

        var levels = new[]
        {
            AgentSessionContextPolicyLevel.Off,
            AgentSessionContextPolicyLevel.Minimal,
            AgentSessionContextPolicyLevel.Standard,
            AgentSessionContextPolicyLevel.Detailed,
        };

        var tasks = Enumerable.Range(0, 8).Select(i => Task.Run(() =>
        {
            var level = levels[i % levels.Length];
            sessionService.TrySetSessionOverride(conversationId, level);
            return SendAsync(sessionService, backend, conversationId).GetAwaiter().GetResult();
        })).ToArray();

        await Task.WhenAll(tasks);

        var state = sessionService.GetPolicyState(conversationId);
        Assert.Equal(AgentSessionContextPolicyLevel.Standard, state.ApplicationDefaultLevel);
        Assert.True(state.IsOverrideActive);
        Assert.Contains(state.EffectiveLevel, levels);
    }

    private static AgentSessionService CreateSessionService(FakeAgentBackend? backend = null)
    {
        backend ??= new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        return new AgentSessionService(
            new[] { backend },
            new AgentEventStream(),
            contextManifestBuilder: new AgentContextManifestBuilder(),
            contextSnapshotSources: CreateDeterministicSnapshotSources());
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

    private static Task<AgentRunSnapshot> SendAsync(
        AgentSessionService sessionService,
        FakeAgentBackend backend,
        ConversationId conversationId) =>
        sessionService.SendAsync(
            conversationId,
            ActorId.HumanUser,
            ActorId.PanelSeed("test"),
            backend.BackendId,
            ConversationEntryId.New(),
            "message",
            CancellationToken.None);

    // ── M6 adversarial tests ──────────────────────────────────────────

    [Fact]
    public async Task EndAsync_RetainsSessionPolicyOverride_ForReusedConversationId()
    {
        // M6: After EndAsync, the session is destroyed but the in-memory
        // policy override is NOT cleared. If the same ConversationId is
        // reused for a new session, the previous override persists.
        // This is by design: policy overrides are conversation-scoped,
        // not session-scoped. Persistence is deferred to a later phase.
        var backend = new FakeAgentBackend(AgentBackendIds.LegacyOpenAiCompatible);
        backend.SetCompletion("done");
        var sessionService = CreateSessionService(backend);
        var conversationId = ConversationId.NewDirect();

        // Set an override and verify it takes effect
        sessionService.TrySetSessionOverride(
            conversationId, AgentSessionContextPolicyLevel.Minimal);
        await SendAsync(sessionService, backend, conversationId);
        var firstManifest = backend.LastExecutionContext!.ContextManifest;
        Assert.NotNull(firstManifest);
        Assert.Equal(AgentContextPolicyLevel.Minimal, firstManifest.PolicyLevelApplied);

        // End the session
        await sessionService.EndAsync(conversationId);

        // After EndAsync, the policy state for this conversation still
        // reflects the override (it was not cleared by EndAsync)
        var state = sessionService.GetPolicyState(conversationId);
        Assert.Equal(AgentSessionContextPolicyLevel.Minimal, state.EffectiveLevel);
        Assert.True(state.IsOverrideActive);

        // Clear the override explicitly
        sessionService.ClearSessionOverride(conversationId);
        var clearedState = sessionService.GetPolicyState(conversationId);
        Assert.False(clearedState.IsOverrideActive);
        Assert.Equal(AgentSessionContextPolicyLevel.Standard, clearedState.EffectiveLevel);
    }

    private static AgentContextPolicyLevel MapToDomain(AgentSessionContextPolicyLevel level) =>
        level switch
        {
            AgentSessionContextPolicyLevel.Off => AgentContextPolicyLevel.Off,
            AgentSessionContextPolicyLevel.Minimal => AgentContextPolicyLevel.Minimal,
            AgentSessionContextPolicyLevel.Standard => AgentContextPolicyLevel.Standard,
            AgentSessionContextPolicyLevel.Detailed => AgentContextPolicyLevel.Detailed,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
        };
}
