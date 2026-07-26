using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Debugging.Application;
using Zaide.Features.Editor.Application;
using Zaide.Features.Language.Application;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.SourceControl.Application;
using Zaide.Features.SourceControl.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Serializes contract-level IDE snapshots into deterministic context text.
/// </summary>
internal static class AgentContextContentComposer
{
    private static readonly Regex EnvironmentLinePattern = new(
        @"^(?:[A-Z_][A-Z0-9_]*=|(?:export\s+)?[A-Z_][A-Z0-9_]*=|Environment\.|Process\.Environment)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static AgentContextRawContent? TryCompose(
        AgentContextSourceId sourceId,
        IAgentContextSnapshotSources snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        return sourceId switch
        {
            var id when id == AgentContextSourceId.BuildTestFailure =>
                ComposeBuildTestFailure(snapshots),
            var id when id == AgentContextSourceId.DebugException =>
                ComposeDebugException(snapshots),
            var id when id == AgentContextSourceId.ProjectContext =>
                ComposeProjectContext(snapshots),
            var id when id == AgentContextSourceId.ActiveFile =>
                ComposeActiveFile(snapshots),
            var id when id == AgentContextSourceId.LanguageDiagnostics =>
                ComposeLanguageDiagnostics(snapshots),
            var id when id == AgentContextSourceId.BuildDiagnostics =>
                ComposeBuildDiagnostics(snapshots),
            var id when id == AgentContextSourceId.TestResultsSummary =>
                ComposeTestResultsSummary(snapshots),
            var id when id == AgentContextSourceId.WorkflowState =>
                ComposeWorkflowState(snapshots),
            var id when id == AgentContextSourceId.OpenFiles =>
                ComposeOpenFiles(snapshots),
            var id when id == AgentContextSourceId.SourceControlSummary =>
                ComposeSourceControlSummary(snapshots),
            var id when id == AgentContextSourceId.DebugSessionState =>
                ComposeDebugSessionState(snapshots),
            var id when id == AgentContextSourceId.EditorCaretSelection =>
                ComposeEditorCaretSelection(snapshots),
            _ => throw new ArgumentOutOfRangeException(nameof(sourceId), sourceId, "Unknown source id."),
        };
    }

    public static bool IsBinaryContent(string? content) =>
        !string.IsNullOrEmpty(content) && content.Contains('\0', StringComparison.Ordinal);

    private static AgentContextRawContent ComposeBuildTestFailure(IAgentContextSnapshotSources snapshots)
    {
        var workflow = snapshots.Workflow;
        var buildDiagnostics = snapshots.BuildDiagnostics;
        var testResults = snapshots.TestResults;
        var builder = new StringBuilder();

        if (workflow.LastOutcome is ProjectWorkflowOutcomeKind.Failed
            or ProjectWorkflowOutcomeKind.StartupFailed)
        {
            builder.AppendLine($"workflow-outcome={workflow.LastOutcome}");
            builder.AppendLine($"workflow-target={workflow.TargetFilePath}");
            AppendFilteredOutputLines(builder, workflow.OutputLines);
        }

        foreach (var diagnostic in buildDiagnostics.Diagnostics
                     .Where(item => item.Severity == LanguageDiagnosticSeverity.Error)
                     .OrderBy(item => item.FilePath, StringComparer.Ordinal)
                     .ThenBy(item => item.Line)
                     .ThenBy(item => item.Column))
        {
            builder.AppendLine(
                $"{diagnostic.FilePath}:{diagnostic.Line}:{diagnostic.Column} {diagnostic.Code} {diagnostic.Message}");
        }

        if (testResults.Summary?.Failed is > 0)
        {
            builder.AppendLine(
                $"test-summary failed={testResults.Summary.Failed} total={testResults.Summary.Total}");
            foreach (var testCase in testResults.Cases
                         .Where(item => item.Outcome == TestCaseOutcome.Failed)
                         .OrderBy(item => item.DisplayName, StringComparer.Ordinal))
            {
                builder.AppendLine($"{testCase.DisplayName}: {testCase.ErrorMessage}");
            }
        }

        var content = builder.ToString().TrimEnd();
        if (content.Length == 0)
        {
            return AgentContextRawContent.NoAttachableContent(
                AgentContextSourceId.BuildTestFailure,
                "service:project-workflow",
                workflow.Generation,
                "build-test-failure");
        }

        return AgentContextRawContent.Attachable(
            AgentContextSourceId.BuildTestFailure,
            content,
            "build-test-failure",
            "service:project-workflow",
            Math.Max(workflow.Generation, Math.Max(buildDiagnostics.BuildGeneration, testResults.Generation)));
    }

    private static AgentContextRawContent ComposeDebugException(IAgentContextSnapshotSources snapshots)
    {
        var debugSession = snapshots.DebugSession;

        if (debugSession.State == DebugSessionState.Unavailable)
        {
            return AgentContextRawContent.Unavailable(
                AgentContextSourceId.DebugException,
                "service:debug-session",
                debugSession.Generation,
                "debug-exception");
        }

        var builder = new StringBuilder();
        if (debugSession.State == DebugSessionState.Failed && debugSession.Failure is not null)
        {
            builder.AppendLine($"failure-kind={debugSession.Failure.Kind}");
            builder.AppendLine(debugSession.Failure.Message);
        }
        else if (debugSession.State == DebugSessionState.Stopped && debugSession.StopInfo is not null)
        {
            builder.AppendLine($"stop-reason={debugSession.StopInfo.Reason}");
            builder.AppendLine($"thread-id={debugSession.StopInfo.ThreadId}");
        }

        var content = builder.ToString().TrimEnd();
        if (content.Length == 0)
        {
            return AgentContextRawContent.NoAttachableContent(
                AgentContextSourceId.DebugException,
                "service:debug-session",
                debugSession.Generation,
                "debug-exception");
        }

        return AgentContextRawContent.Attachable(
            AgentContextSourceId.DebugException,
            content,
            "debug-exception",
            "service:debug-session",
            debugSession.Generation);
    }

    private static AgentContextRawContent ComposeProjectContext(IAgentContextSnapshotSources snapshots)
    {
        var projectContext = snapshots.ProjectContext;
        if (projectContext.State == ProjectContextState.Unloaded)
        {
            return AgentContextRawContent.Unavailable(
                AgentContextSourceId.ProjectContext,
                "service:project-context",
                snapshotGeneration: 0,
                "project-context");
        }

        var builder = new StringBuilder();
        builder.AppendLine($"state={projectContext.State}");
        builder.AppendLine($"workspace-root={projectContext.WorkspaceRoot}");
        if (projectContext.SelectedProject is not null)
        {
            builder.AppendLine($"selected={projectContext.SelectedProject.FilePath}");
        }

        foreach (var candidate in projectContext.Candidates.OrderBy(
                     item => item.FilePath,
                     StringComparer.Ordinal))
        {
            builder.AppendLine($"candidate={candidate.FilePath}");
        }

        if (!string.IsNullOrWhiteSpace(projectContext.ErrorMessage))
        {
            builder.AppendLine(projectContext.ErrorMessage);
        }

        return AgentContextRawContent.Attachable(
            AgentContextSourceId.ProjectContext,
            builder.ToString().TrimEnd(),
            "project-context",
            "service:project-context",
            snapshotGeneration: 0);
    }

    private static AgentContextRawContent ComposeActiveFile(IAgentContextSnapshotSources snapshots)
    {
        var editor = snapshots.Editor;
        if (string.IsNullOrWhiteSpace(editor.ActiveFilePath))
        {
            return AgentContextRawContent.NoAttachableContent(
                AgentContextSourceId.ActiveFile,
                "service:editor-state-snapshot",
                editor.Generation,
                "active-file");
        }

        if (IsBinaryContent(editor.ActiveFileContent))
        {
            return AgentContextRawContent.HardExcluded(
                AgentContextSourceId.ActiveFile,
                AgentContextHardExclusionId.BinaryFileContent,
                "service:editor-state-snapshot",
                editor.Generation,
                "active-file");
        }

        var builder = new StringBuilder();
        builder.AppendLine($"path={editor.ActiveFilePath}");
        builder.AppendLine($"dirty={editor.ActiveFileIsDirty}");
        if (!string.IsNullOrEmpty(editor.ActiveFileContent))
        {
            builder.AppendLine(editor.ActiveFileContent);
        }

        return AgentContextRawContent.Attachable(
            AgentContextSourceId.ActiveFile,
            builder.ToString().TrimEnd(),
            "active-file",
            "service:editor-state-snapshot",
            editor.Generation);
    }

    private static AgentContextRawContent ComposeLanguageDiagnostics(IAgentContextSnapshotSources snapshots)
    {
        var diagnosticsSnapshot = snapshots.LanguageDiagnostics;
        if (diagnosticsSnapshot.State == LanguageSessionState.Unavailable)
        {
            return AgentContextRawContent.Unavailable(
                AgentContextSourceId.LanguageDiagnostics,
                "service:language-diagnostics",
                diagnosticsSnapshot.SessionGeneration,
                "language-diagnostics");
        }

        var activeFilePath = snapshots.Editor.ActiveFilePath;
        var builder = new StringBuilder();
        foreach (var diagnostic in diagnosticsSnapshot.Diagnostics
                     .Where(item => string.IsNullOrWhiteSpace(activeFilePath)
                         || string.Equals(item.FilePath, activeFilePath, StringComparison.Ordinal))
                     .OrderBy(item => item.FilePath, StringComparer.Ordinal)
                     .ThenBy(item => item.Range.StartLine)
                     .ThenBy(item => item.Range.StartCharacter))
        {
            builder.AppendLine(
                $"{diagnostic.FilePath}:{diagnostic.Range.StartLine + 1}:{diagnostic.Range.StartCharacter + 1} " +
                $"{diagnostic.Severity} {diagnostic.Code} {diagnostic.Message}");
        }

        var content = builder.ToString().TrimEnd();
        if (content.Length == 0)
        {
            return AgentContextRawContent.NoAttachableContent(
                AgentContextSourceId.LanguageDiagnostics,
                "service:language-diagnostics",
                diagnosticsSnapshot.SessionGeneration,
                "language-diagnostics");
        }

        return AgentContextRawContent.Attachable(
            AgentContextSourceId.LanguageDiagnostics,
            content,
            "language-diagnostics",
            "service:language-diagnostics",
            diagnosticsSnapshot.SessionGeneration);
    }

    private static AgentContextRawContent ComposeBuildDiagnostics(IAgentContextSnapshotSources snapshots)
    {
        var buildDiagnostics = snapshots.BuildDiagnostics;
        var builder = new StringBuilder();
        foreach (var diagnostic in buildDiagnostics.Diagnostics
                     .OrderBy(item => item.FilePath, StringComparer.Ordinal)
                     .ThenBy(item => item.Line)
                     .ThenBy(item => item.Column))
        {
            builder.AppendLine(
                $"{diagnostic.FilePath}:{diagnostic.Line}:{diagnostic.Column} {diagnostic.Severity} {diagnostic.Code} {diagnostic.Message}");
        }

        var content = builder.ToString().TrimEnd();
        if (content.Length == 0)
        {
            return AgentContextRawContent.NoAttachableContent(
                AgentContextSourceId.BuildDiagnostics,
                "service:build-diagnostics",
                buildDiagnostics.BuildGeneration,
                "build-diagnostics");
        }

        return AgentContextRawContent.Attachable(
            AgentContextSourceId.BuildDiagnostics,
            content,
            "build-diagnostics",
            "service:build-diagnostics",
            buildDiagnostics.BuildGeneration);
    }

    private static AgentContextRawContent ComposeTestResultsSummary(IAgentContextSnapshotSources snapshots)
    {
        var testResults = snapshots.TestResults;
        if (testResults.Summary is null && testResults.Cases.Count == 0)
        {
            return AgentContextRawContent.NoAttachableContent(
                AgentContextSourceId.TestResultsSummary,
                "service:test-results",
                testResults.Generation,
                "test-results-summary");
        }

        var builder = new StringBuilder();
        if (testResults.Summary is not null)
        {
            builder.AppendLine(
                $"passed={testResults.Summary.Passed} failed={testResults.Summary.Failed} " +
                $"skipped={testResults.Summary.Skipped} total={testResults.Summary.Total}");
        }

        foreach (var testCase in testResults.Cases.OrderBy(item => item.DisplayName, StringComparer.Ordinal))
        {
            builder.AppendLine($"{testCase.Outcome} {testCase.DisplayName} {testCase.Duration}");
        }

        return AgentContextRawContent.Attachable(
            AgentContextSourceId.TestResultsSummary,
            builder.ToString().TrimEnd(),
            "test-results-summary",
            "service:test-results",
            testResults.Generation);
    }

    private static AgentContextRawContent ComposeWorkflowState(IAgentContextSnapshotSources snapshots)
    {
        var workflow = snapshots.Workflow;
        var builder = new StringBuilder();
        builder.AppendLine($"state={workflow.State}");
        builder.AppendLine($"generation={workflow.Generation}");
        builder.AppendLine($"active-operation={workflow.ActiveOperation}");
        builder.AppendLine($"last-outcome={workflow.LastOutcome}");
        builder.AppendLine($"target={workflow.TargetFilePath}");
        AppendFilteredOutputLines(builder, workflow.OutputLines);

        return AgentContextRawContent.Attachable(
            AgentContextSourceId.WorkflowState,
            builder.ToString().TrimEnd(),
            "workflow-state",
            "service:project-workflow",
            workflow.Generation);
    }

    private static AgentContextRawContent ComposeOpenFiles(IAgentContextSnapshotSources snapshots)
    {
        var editor = snapshots.Editor;
        if (editor.OpenFilePaths.Count == 0)
        {
            return AgentContextRawContent.NoAttachableContent(
                AgentContextSourceId.OpenFiles,
                "service:editor-state-snapshot",
                editor.Generation,
                "open-files");
        }

        var builder = new StringBuilder();
        foreach (var path in editor.OpenFilePaths.OrderBy(path => path, StringComparer.Ordinal))
        {
            builder.AppendLine(path);
        }

        return AgentContextRawContent.Attachable(
            AgentContextSourceId.OpenFiles,
            builder.ToString().TrimEnd(),
            "open-files",
            "service:editor-state-snapshot",
            editor.Generation);
    }

    private static AgentContextRawContent ComposeSourceControlSummary(IAgentContextSnapshotSources snapshots)
    {
        var sourceControl = snapshots.SourceControl;
        if (sourceControl.Availability != SourceControlSnapshotAvailability.Available)
        {
            return AgentContextRawContent.Unavailable(
                AgentContextSourceId.SourceControlSummary,
                "service:source-control-snapshot",
                sourceControl.Generation,
                "source-control-summary");
        }

        var builder = new StringBuilder();
        builder.AppendLine($"branch={sourceControl.RepositoryStatus?.CurrentBranchName}");
        builder.AppendLine($"ahead={sourceControl.RepositoryStatus?.AheadBy}");
        builder.AppendLine($"behind={sourceControl.RepositoryStatus?.BehindBy}");
        foreach (var change in sourceControl.RepositoryStatus?.Changes
                     .OrderBy(item => item.FilePath, StringComparer.Ordinal)
                     ?? Enumerable.Empty<FileChange>())
        {
            builder.AppendLine($"{change.ChangeType} {change.FilePath}");
        }

        return AgentContextRawContent.Attachable(
            AgentContextSourceId.SourceControlSummary,
            builder.ToString().TrimEnd(),
            "source-control-summary",
            "service:source-control-snapshot",
            sourceControl.Generation);
    }

    private static AgentContextRawContent ComposeDebugSessionState(IAgentContextSnapshotSources snapshots)
    {
        var debugSession = snapshots.DebugSession;
        if (debugSession.State == DebugSessionState.Unavailable)
        {
            return AgentContextRawContent.Unavailable(
                AgentContextSourceId.DebugSessionState,
                "service:debug-session",
                debugSession.Generation,
                "debug-session-state");
        }

        var builder = new StringBuilder();
        builder.AppendLine($"state={debugSession.State}");
        builder.AppendLine($"program={debugSession.ProgramPath}");
        builder.AppendLine($"working-directory={debugSession.WorkingDirectory}");
        builder.AppendLine($"last-outcome={debugSession.LastOutcome}");
        if (debugSession.StopInfo is not null)
        {
            builder.AppendLine($"stop-reason={debugSession.StopInfo.Reason}");
            builder.AppendLine($"thread-id={debugSession.StopInfo.ThreadId}");
        }

        foreach (var line in debugSession.DiagnosticOutput.OrderBy(line => line, StringComparer.Ordinal))
        {
            builder.AppendLine(line);
        }

        return AgentContextRawContent.Attachable(
            AgentContextSourceId.DebugSessionState,
            builder.ToString().TrimEnd(),
            "debug-session-state",
            "service:debug-session",
            debugSession.Generation);
    }

    private static AgentContextRawContent ComposeEditorCaretSelection(IAgentContextSnapshotSources snapshots)
    {
        var editor = snapshots.Editor;
        if (string.IsNullOrWhiteSpace(editor.ActiveFilePath))
        {
            return AgentContextRawContent.NoAttachableContent(
                AgentContextSourceId.EditorCaretSelection,
                "service:editor-state-snapshot",
                editor.Generation,
                "editor-caret-selection");
        }

        var builder = new StringBuilder();
        builder.AppendLine($"path={editor.ActiveFilePath}");
        builder.AppendLine($"caret={editor.CaretLine}:{editor.CaretColumn}");
        builder.AppendLine($"selection-start={editor.SelectionStart}");
        builder.AppendLine($"selection-length={editor.SelectionLength}");
        if (!string.IsNullOrEmpty(editor.SelectionText))
        {
            builder.AppendLine($"selection-text={editor.SelectionText}");
        }

        return AgentContextRawContent.Attachable(
            AgentContextSourceId.EditorCaretSelection,
            builder.ToString().TrimEnd(),
            "editor-caret-selection",
            "service:editor-state-snapshot",
            editor.Generation);
    }

    private static void AppendFilteredOutputLines(
        StringBuilder builder,
        IReadOnlyList<ManagedProcessOutputLine> outputLines)
    {
        foreach (var line in outputLines
                     .OrderBy(item => item.Generation)
                     .ThenBy(item => item.Stream)
                     .ThenBy(item => item.Text, StringComparer.Ordinal)
                     .ThenBy(item => item.Timestamp)
                     .Select(item => item.Text))
        {
            if (EnvironmentLinePattern.IsMatch(line))
            {
                continue;
            }

            builder.AppendLine(line);
        }
    }
}

/// <summary>
/// Raw serialized content extracted from one snapshot source.
/// </summary>
internal sealed class AgentContextRawContent
{
    private AgentContextRawContent(
        AgentContextSourceId sourceId,
        string? content,
        string scopeDescriptor,
        string sourceServiceIdentity,
        long snapshotGeneration,
        AgentContextRawContentStatus status,
        AgentContextHardExclusionId? hardExclusionId)
    {
        SourceId = sourceId;
        Content = content;
        ScopeDescriptor = scopeDescriptor;
        SourceServiceIdentity = sourceServiceIdentity;
        SnapshotGeneration = snapshotGeneration;
        Status = status;
        HardExclusionId = hardExclusionId;
    }

    public AgentContextSourceId SourceId { get; }

    public string? Content { get; }

    public string ScopeDescriptor { get; }

    public string SourceServiceIdentity { get; }

    public long SnapshotGeneration { get; }

    public AgentContextRawContentStatus Status { get; }

    public AgentContextHardExclusionId? HardExclusionId { get; }

    public static AgentContextRawContent Attachable(
        AgentContextSourceId sourceId,
        string content,
        string scopeDescriptor,
        string sourceServiceIdentity,
        long snapshotGeneration) =>
        new(
            sourceId,
            content,
            scopeDescriptor,
            sourceServiceIdentity,
            snapshotGeneration,
            AgentContextRawContentStatus.Attachable,
            hardExclusionId: null);

    public static AgentContextRawContent NoAttachableContent(
        AgentContextSourceId sourceId,
        string sourceServiceIdentity,
        long snapshotGeneration,
        string scopeDescriptor) =>
        new(
            sourceId,
            content: null,
            scopeDescriptor,
            sourceServiceIdentity,
            snapshotGeneration,
            AgentContextRawContentStatus.NoAttachableContent,
            hardExclusionId: null);

    public static AgentContextRawContent Unavailable(
        AgentContextSourceId sourceId,
        string sourceServiceIdentity,
        long snapshotGeneration,
        string scopeDescriptor) =>
        new(
            sourceId,
            content: null,
            scopeDescriptor,
            sourceServiceIdentity,
            snapshotGeneration,
            AgentContextRawContentStatus.Unavailable,
            hardExclusionId: null);

    public static AgentContextRawContent HardExcluded(
        AgentContextSourceId sourceId,
        AgentContextHardExclusionId hardExclusionId,
        string sourceServiceIdentity,
        long snapshotGeneration,
        string scopeDescriptor) =>
        new(
            sourceId,
            content: null,
            scopeDescriptor,
            sourceServiceIdentity,
            snapshotGeneration,
            AgentContextRawContentStatus.HardExcluded,
            hardExclusionId);
}

internal enum AgentContextRawContentStatus
{
    Attachable,
    NoAttachableContent,
    Unavailable,
    HardExcluded,
}
