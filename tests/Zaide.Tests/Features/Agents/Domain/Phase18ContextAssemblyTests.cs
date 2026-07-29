using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Debugging.Application;
using Zaide.Features.Editor.Application;
using Zaide.Features.Language.Application;
using Zaide.Features.Language.Infrastructure.Lsp;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.SourceControl.Application;
using Zaide.Features.SourceControl.Domain;

namespace Zaide.Tests.Features.Agents.Domain;

public sealed class Phase18ContextAssemblyTests
{
    private static readonly DateTimeOffset FixedAssemblyTime =
        new(2026, 7, 26, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PolicyEvaluation_ApplicationDefaultIsStandard()
    {
        var service = new AgentContextPolicyEvaluationService();
        var result = service.Evaluate(AgentContextPolicy.CreateApplicationDefault());

        Assert.Equal(AgentContextPolicyLevel.Standard, result.EffectiveLevel);
        Assert.Contains(AgentContextSourceId.ActiveFile, result.IncludedSources);
        Assert.DoesNotContain(AgentContextSourceId.OpenFiles, result.IncludedSources);
    }

    [Fact]
    public void PolicyEvaluation_SessionOverrideTakesPrecedenceOverApplicationDefault()
    {
        var service = new AgentContextPolicyEvaluationService();
        var policy = new AgentContextPolicy(
            AgentContextPolicyLevel.Standard,
            new AgentContextSessionOverride(AgentContextPolicyLevel.Minimal));

        var result = service.Evaluate(policy);

        Assert.Equal(AgentContextPolicyLevel.Minimal, result.EffectiveLevel);
        Assert.Contains(AgentContextSourceId.ProjectContext, result.IncludedSources);
        Assert.DoesNotContain(AgentContextSourceId.ActiveFile, result.IncludedSources);
    }

    [Fact]
    public void PolicyEvaluation_OffPolicyExcludesAllSources()
    {
        AssertPolicySourceCount(AgentContextPolicyLevel.Off, expectedIncludedCount: 0);
    }

    [Fact]
    public void PolicyEvaluation_MinimalPolicyIncludesThreeSources()
    {
        AssertPolicySourceCount(AgentContextPolicyLevel.Minimal, expectedIncludedCount: 3);
    }

    [Fact]
    public void PolicyEvaluation_StandardPolicyIncludesNineSources()
    {
        // M5 added the DurableMemory context source under the Standard level,
        // expanding the included set from the original eight to nine.
        // The total source set is still AgentContextSourceId.All; the
        // exclusion count is reduced accordingly.
        AssertPolicySourceCount(AgentContextPolicyLevel.Standard, expectedIncludedCount: 9);
    }

    [Fact]
    public void PolicyEvaluation_DetailedPolicyIncludesThirteenSources()
    {
        // M5 added the DurableMemory context source, which is included at
        // Detailed as well, expanding the included set from the original
        // twelve to thirteen. The total source set is still
        // AgentContextSourceId.All; the exclusion count is reduced accordingly.
        AssertPolicySourceCount(AgentContextPolicyLevel.Detailed, expectedIncludedCount: 13);
    }

    private static void AssertPolicySourceCount(
        AgentContextPolicyLevel level,
        int expectedIncludedCount)
    {
        var service = new AgentContextPolicyEvaluationService();
        var policy = new AgentContextPolicy(level);

        var result = service.Evaluate(policy);

        Assert.Equal(expectedIncludedCount, result.IncludedSources.Count);
        Assert.Equal(
            AgentContextSourceId.All.Count - expectedIncludedCount,
            result.PolicyExclusionDecisions.Count);
    }

    [Fact]
    public void ManifestBuilder_OffPolicyProducesEmptyManifestWithPolicyExclusions()
    {
        var builder = new AgentContextManifestBuilder();
        var snapshots = CreateDetailedSnapshots();

        var manifest = builder.Build(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            new AgentContextPolicy(AgentContextPolicyLevel.Off),
            snapshots,
            FixedAssemblyTime);

        Assert.Empty(manifest.Items);
        Assert.Equal(AgentContextPolicyLevel.Off, manifest.PolicyLevelApplied);
        Assert.Equal(0, manifest.TokenBudget.RequestedBudget);
        Assert.Equal(AgentContextSourceId.All.Count, manifest.ExclusionDecisions.Count);
        Assert.All(
            manifest.ExclusionDecisions,
            decision => Assert.False(decision.IsHardExclusion));
    }

    [Fact]
    public void ManifestBuilder_MinimalPolicyIncludesOnlyMinimalSources()
    {
        var builder = new AgentContextManifestBuilder();
        var snapshots = CreateDetailedSnapshots(includeFailure: true);

        var manifest = BuildWithPolicy(builder, snapshots, AgentContextPolicyLevel.Minimal);

        Assert.Equal(
            new[]
            {
                AgentContextSourceId.BuildTestFailure,
                AgentContextSourceId.ProjectContext,
            },
            manifest.Items.Select(item => item.SourceId).ToArray());
    }

    [Fact]
    public void ManifestBuilder_StandardPolicyIncludesStandardMatrixSources()
    {
        var builder = new AgentContextManifestBuilder();
        var snapshots = CreateDetailedSnapshots(includeFailure: true);

        var manifest = BuildWithPolicy(builder, snapshots, AgentContextPolicyLevel.Standard);

        var expected = new[]
        {
            AgentContextSourceId.BuildTestFailure,
            AgentContextSourceId.ActiveFile,
            AgentContextSourceId.LanguageDiagnostics,
            AgentContextSourceId.TestResultsSummary,
            AgentContextSourceId.WorkflowState,
            AgentContextSourceId.BuildDiagnostics,
            AgentContextSourceId.ProjectContext,
        };

        Assert.Equal(expected, manifest.Items.Select(item => item.SourceId).ToArray());
    }

    [Fact]
    public void ManifestBuilder_DetailedPolicyIncludesDetailedOnlySources()
    {
        var builder = new AgentContextManifestBuilder();
        var snapshots = CreateDetailedSnapshots(includeFailure: true, includeDebugStop: true);

        var manifest = BuildWithPolicy(builder, snapshots, AgentContextPolicyLevel.Detailed);

        Assert.Contains(AgentContextSourceId.OpenFiles, manifest.Items.Select(item => item.SourceId));
        Assert.Contains(
            AgentContextSourceId.SourceControlSummary,
            manifest.Items.Select(item => item.SourceId));
        Assert.Contains(
            AgentContextSourceId.DebugSessionState,
            manifest.Items.Select(item => item.SourceId));
        Assert.Contains(
            AgentContextSourceId.EditorCaretSelection,
            manifest.Items.Select(item => item.SourceId));
    }

    [Fact]
    public void ManifestBuilder_HardExcludesBinaryActiveFileContent()
    {
        var builder = new AgentContextManifestBuilder();
        var snapshots = CreateDetailedSnapshots();
        snapshots = snapshots with
        {
            Editor = new EditorStateSnapshot(
                generation: 3,
                activeFilePath: "/workspace/image.bin",
                activeFileContent: "binary\0payload"),
        };

        var manifest = BuildWithPolicy(builder, snapshots, AgentContextPolicyLevel.Standard);

        Assert.DoesNotContain(
            manifest.Items,
            item => item.SourceId == AgentContextSourceId.ActiveFile);
        Assert.Contains(
            manifest.ExclusionDecisions,
            decision => decision.IsHardExclusion
                && decision.HardExclusionId == AgentContextHardExclusionId.BinaryFileContent);
    }

    [Fact]
    public void ManifestBuilder_RecordsUnavailableSourceControlCapability()
    {
        var builder = new AgentContextManifestBuilder();
        var snapshots = CreateDetailedSnapshots(includeFailure: true) with
        {
            SourceControl = new SourceControlStatusSnapshot(
                generation: 2,
                availability: SourceControlSnapshotAvailability.NotARepository),
        };

        var manifest = BuildWithPolicy(builder, snapshots, AgentContextPolicyLevel.Detailed);

        Assert.Contains(
            manifest.ExclusionDecisions,
            decision => decision.SourceId == AgentContextSourceId.SourceControlSummary
                && decision.Reason.Contains("unavailable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ManifestBuilder_RecordsUnavailableLanguageDiagnosticsCapability()
    {
        var builder = new AgentContextManifestBuilder();
        var snapshots = CreateDetailedSnapshots() with
        {
            LanguageDiagnostics = LanguageDiagnosticsSnapshot.Empty,
        };

        var manifest = BuildWithPolicy(builder, snapshots, AgentContextPolicyLevel.Standard);

        Assert.Contains(
            manifest.ExclusionDecisions,
            decision => decision.SourceId == AgentContextSourceId.LanguageDiagnostics
                && decision.Reason.Contains("unavailable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ManifestBuilder_DeterministicOrderingMatchesPriorityContract()
    {
        var builder = new AgentContextManifestBuilder();
        var snapshots = CreateDetailedSnapshots(includeFailure: true, includeDebugStop: true);

        var manifest = BuildWithPolicy(builder, snapshots, AgentContextPolicyLevel.Detailed);
        var priorities = manifest.Items
            .Select(item => AgentContextSourcePriority.GetPriority(item.SourceId))
            .ToArray();

        Assert.Equal(priorities.OrderBy(priority => priority).ToArray(), priorities);
    }

    [Fact]
    public void ManifestBuilder_RedactionRunsBeforeTokenCounting()
    {
        var builder = new AgentContextManifestBuilder();
        var secret = new string('a', 40);
        var snapshots = CreateDetailedSnapshots() with
        {
            Editor = new EditorStateSnapshot(
                generation: 4,
                activeFilePath: "/workspace/Program.cs",
                activeFileContent: $"class Program {{ const string Key = \"sk-{secret}\"; }}"),
        };

        var manifest = BuildWithPolicy(builder, snapshots, AgentContextPolicyLevel.Standard);
        var activeFile = manifest.Items.Single(item => item.SourceId == AgentContextSourceId.ActiveFile);

        Assert.Contains("[REDACTED:api-key]", activeFile.Content, StringComparison.Ordinal);
        Assert.Equal(
            AgentContextTokenEstimator.Estimate(activeFile.Content),
            activeFile.EstimatedTokenCount);
        Assert.True(activeFile.Provenance.RedactionApplied);
    }

    [Fact]
    public void ManifestBuilder_FailClosedRedactionDropsItemWithoutEmittingRawContent()
    {
        var outcome = AgentContextRedactionProcessor.Apply(null!);

        Assert.True(outcome.DidProcessingFail);
        Assert.Equal(string.Empty, outcome.Content);
        Assert.Equal(AgentContextRedactionState.ProcessingFailed, outcome.State);
    }

    [Fact]
    public void ManifestBuilder_BudgetOverflowDropsLowestPriorityItemsAtomically()
    {
        var builder = new AgentContextManifestBuilder();
        var snapshots = CreateDetailedSnapshots(includeFailure: true) with
        {
            Editor = new EditorStateSnapshot(
                generation: 5,
                activeFilePath: "/workspace/Program.cs",
                activeFileContent: new string('x', 12_000),
                openFilePaths: new[] { "/workspace/Program.cs", "/workspace/Other.cs" },
                caretLine: 1,
                caretColumn: 1,
                selectionStart: 0,
                selectionLength: 0),
            Workflow = CreateDetailedSnapshots(includeFailure: true).Workflow with
            {
                OutputLines =
                [
                    new ManagedProcessOutputLine(
                        11,
                        ProcessStreamKind.StdOut,
                        new string('y', 12_000),
                        FixedAssemblyTime),
                ],
            },
        };

        var manifest = BuildWithPolicy(builder, snapshots, AgentContextPolicyLevel.Standard);

        Assert.True(manifest.TotalEstimatedTokenCount <= 4_000);
        Assert.Contains(
            manifest.TruncationDecisions,
            decision => decision.ItemDropped
                && decision.Reason.Contains("budget overflow", StringComparison.Ordinal));
        Assert.DoesNotContain(
            manifest.Items,
            item => item.SourceId == AgentContextSourceId.ProjectContext);
    }

    [Fact]
    public void ManifestBuilder_SingleItemOverflowIncludesTruncationMarker()
    {
        var candidates = new[]
        {
            CreateCandidate(AgentContextSourceId.ActiveFile, new string('a', 20_000), priority: 2),
        };

        var result = AgentContextBudgetEnforcer.Apply(candidates, requestedBudget: 4_000);
        var item = Assert.Single(result.Items);

        Assert.Contains(
            AgentContextTokenEstimator.ExceededBudgetMarker,
            item.Content,
            StringComparison.Ordinal);
        Assert.Contains(
            result.TruncationDecisions,
            decision => decision.ItemTruncated
                && decision.SourceId == AgentContextSourceId.ActiveFile);
    }

    [Fact]
    public void ManifestBuilder_BudgetBoundaryIncludesAllItemsWhenWithinBudget()
    {
        var candidates = new[]
        {
            CreateCandidate(AgentContextSourceId.BuildTestFailure, new string('a', 100), priority: 1),
            CreateCandidate(AgentContextSourceId.ActiveFile, new string('b', 100), priority: 2),
        };

        var result = AgentContextBudgetEnforcer.Apply(candidates, requestedBudget: 4_000);

        Assert.Equal(2, result.Items.Count);
        Assert.Empty(result.TruncationDecisions);
        Assert.Equal(50, result.ActualTokenCount);
    }

    [Fact]
    public void ManifestBuilder_PreservesProvenanceAndExclusionDecisions()
    {
        var builder = new AgentContextManifestBuilder();
        var snapshots = CreateDetailedSnapshots(includeFailure: true) with
        {
            SourceControl = new SourceControlStatusSnapshot(
                generation: 9,
                availability: SourceControlSnapshotAvailability.NotARepository),
        };

        var manifest = BuildWithPolicy(builder, snapshots, AgentContextPolicyLevel.Detailed);

        Assert.Equal(FixedAssemblyTime, manifest.AssembledAtUtc);
        Assert.All(manifest.Items, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Provenance.SourceServiceIdentity));
            Assert.True(item.Provenance.WasLiveSnapshot);
            Assert.False(string.IsNullOrWhiteSpace(item.Fingerprint));
        });
        Assert.Contains(manifest.ExclusionDecisions, decision => decision.SourceId != null);
        Assert.Contains(manifest.ExclusionDecisions, decision => decision.IsHardExclusion == false);
    }

    [Fact]
    public void ManifestBuilder_RepeatedIdenticalInputsProduceIdenticalManifests()
    {
        var builder = new AgentContextManifestBuilder();
        var snapshots = CreateDetailedSnapshots(includeFailure: true, includeDebugStop: true);
        var sessionId = AgentSessionId.New();
        var runId = ExecutionRunId.New();
        var conversationId = ConversationId.NewDirect();
        var policy = new AgentContextPolicy(AgentContextPolicyLevel.Detailed);

        var first = builder.Build(
            sessionId,
            runId,
            conversationId,
            policy,
            snapshots,
            FixedAssemblyTime);
        var second = builder.Build(
            sessionId,
            runId,
            conversationId,
            policy,
            snapshots,
            FixedAssemblyTime);

        Assert.Equal(first.PolicyLevelApplied, second.PolicyLevelApplied);
        Assert.Equal(first.Items.Count, second.Items.Count);
        Assert.Equal(
            first.Items.Select(item => item.SourceId).ToArray(),
            second.Items.Select(item => item.SourceId).ToArray());
        Assert.Equal(
            first.Items.Select(item => item.Content).ToArray(),
            second.Items.Select(item => item.Content).ToArray());
        Assert.Equal(first.TotalEstimatedTokenCount, second.TotalEstimatedTokenCount);
        Assert.Equal(
            first.ExclusionDecisions.Select(decision => decision.Reason).ToArray(),
            second.ExclusionDecisions.Select(decision => decision.Reason).ToArray());
    }

    [Fact]
    public void ManifestBuilder_FiltersEnvironmentLinesFromWorkflowOutput()
    {
        var builder = new AgentContextManifestBuilder();
        var snapshots = CreateDetailedSnapshots() with
        {
            Workflow = CreateDetailedSnapshots().Workflow with
            {
                OutputLines =
                [
                    new ManagedProcessOutputLine(
                        1,
                        ProcessStreamKind.StdOut,
                        "PATH=/secret/path",
                        FixedAssemblyTime),
                    new ManagedProcessOutputLine(
                        1,
                        ProcessStreamKind.StdOut,
                        "Build succeeded.",
                        FixedAssemblyTime),
                ],
            },
        };

        var manifest = BuildWithPolicy(builder, snapshots, AgentContextPolicyLevel.Standard);
        var workflowItem = manifest.Items.Single(item => item.SourceId == AgentContextSourceId.WorkflowState);

        Assert.DoesNotContain("PATH=", workflowItem.Content, StringComparison.Ordinal);
        Assert.Contains("Build succeeded.", workflowItem.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void TokenEstimator_UsesCeilCharacterCountDividedByFour()
    {
        Assert.Equal(1, AgentContextTokenEstimator.Estimate("1234"));
        Assert.Equal(2, AgentContextTokenEstimator.Estimate("12345"));
    }

    private static AgentContextManifest BuildWithPolicy(
        AgentContextManifestBuilder builder,
        TestAgentContextSnapshotSources snapshots,
        AgentContextPolicyLevel level) =>
        builder.Build(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            new AgentContextPolicy(level),
            snapshots,
            FixedAssemblyTime);

    private static AgentContextManifestCandidate CreateCandidate(
        AgentContextSourceId sourceId,
        string content,
        int priority)
    {
        var item = new AgentContextItem(
            sourceId,
            content,
            scopeDescriptor: "test",
            fingerprint: "fp-test",
            AgentContextRedactionState.None,
            AgentContextTokenEstimator.Estimate(content),
            new AgentContextProvenance(
                "service:test",
                snapshotGeneration: 1,
                wasLiveSnapshot: true,
                redactionApplied: false));

        return new AgentContextManifestCandidate(item, priority);
    }

    private static TestAgentContextSnapshotSources CreateDetailedSnapshots(
        bool includeFailure = false,
        bool includeDebugStop = false)
    {
        var workflow = new ProjectWorkflowSnapshot(
            State: ProjectWorkflowOperationState.Idle,
            Generation: 11,
            ActiveOperation: null,
            LastOutcome: includeFailure ? ProjectWorkflowOutcomeKind.Failed : ProjectWorkflowOutcomeKind.Succeeded,
            TargetFilePath: "/workspace/App.csproj",
            ProcessId: null,
            OutputLines:
            [
                new ManagedProcessOutputLine(
                    11,
                    ProcessStreamKind.StdErr,
                    "error CS1002: ; expected",
                    FixedAssemblyTime),
            ],
            LastOperation: ProjectWorkflowOperation.Build);

        var debugSession = includeDebugStop
            ? new DebugSessionSnapshot(
                DebugSessionState.Stopped,
                Generation: 3,
                ProgramPath: "/workspace/bin/App.dll",
                WorkingDirectory: "/workspace",
                AdapterProcessId: 42,
                StopInfo: new("exception", 1),
                Failure: null,
                LastOutcome: null,
                DiagnosticOutput: ["Unhandled exception"],
                BreakpointVerifications: DebugSessionSnapshot.EmptyVerifications)
            : new DebugSessionSnapshot(
                DebugSessionState.Idle,
                Generation: 1,
                ProgramPath: null,
                WorkingDirectory: null,
                AdapterProcessId: null,
                StopInfo: null,
                Failure: null,
                LastOutcome: null,
                DiagnosticOutput: [],
                BreakpointVerifications: DebugSessionSnapshot.EmptyVerifications);

        return new TestAgentContextSnapshotSources
        {
            Editor = new EditorStateSnapshot(
                generation: 2,
                activeFilePath: "/workspace/Program.cs",
                activeFileContent: "class Program {}",
                openFilePaths: new[] { "/workspace/Program.cs", "/workspace/Other.cs" },
                caretLine: 4,
                caretColumn: 8,
                selectionStart: 0,
                selectionLength: 5,
                selectionText: "class"),
            SourceControl = new SourceControlStatusSnapshot(
                generation: 2,
                availability: SourceControlSnapshotAvailability.Available,
                repositoryStatus: new RepositoryStatusSnapshot
                {
                    CurrentBranchName = "main",
                    AheadBy = 1,
                    BehindBy = 0,
                    Changes = [new FileChange("Program.cs", GitChangeType.Modified)],
                }),
            LanguageDiagnostics = new LanguageDiagnosticsSnapshot(
                LanguageSessionState.Ready,
                SessionGeneration: 5,
                Failure: null,
                Diagnostics:
                [
                    new LanguageDiagnostic(
                        "file:///workspace/Program.cs",
                        "/workspace/Program.cs",
                        DocumentVersion: 1,
                        SessionGeneration: 5,
                        LanguageDiagnosticSeverity.Warning,
                        "Unused variable",
                        "CS0219",
                        "csharp-ls",
                        new LspRange(0, 0, 0, 1),
                        StartOffset: 0,
                        EndOffset: 1),
                ]),
            BuildDiagnostics = new BuildDiagnosticsSnapshot(
                BuildGeneration: 11,
                LastOutcome: includeFailure ? ProjectWorkflowOutcomeKind.Failed : null,
                IsPartial: false,
                Diagnostics:
                [
                    new BuildDiagnostic(
                        "/workspace/Program.cs",
                        Line: 1,
                        Column: 1,
                        LanguageDiagnosticSeverity.Error,
                        "CS1002",
                        "; expected"),
                ]),
            Workflow = workflow,
            TestResults = new TestResultsSnapshot(
                Generation: 11,
                OperationOutcome: includeFailure ? ProjectWorkflowOutcomeKind.Failed : ProjectWorkflowOutcomeKind.Succeeded,
                IsPartial: false,
                Summary: new TestResultsSummary(Passed: 1, Failed: includeFailure ? 1 : 0, Skipped: 0, Total: 2),
                Cases:
                [
                    new TestCaseResult(
                        "Tests.App.Test",
                        "App test",
                        includeFailure ? TestCaseOutcome.Failed : TestCaseOutcome.Passed,
                        "10 ms",
                        includeFailure ? "Assert failed" : null,
                        null,
                        "/workspace/Program.cs",
                        10),
                ]),
            DebugSession = debugSession,
            ProjectContext = new ProjectContext(
                ProjectContextState.SingleProject,
                WorkspaceRoot: "/workspace",
                Candidates: [new ProjectCandidate("/workspace/App.csproj", "App", ProjectKind.CSharpProject)],
                SelectedProject: new ProjectCandidate("/workspace/App.csproj", "App", ProjectKind.CSharpProject),
                UnsupportedFiles: [],
                ErrorMessage: null),
        };
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
}
