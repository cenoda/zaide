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

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Read-only snapshot access for IDE context assembly.
/// </summary>
internal interface IAgentContextSnapshotSources
{
    EditorStateSnapshot Editor { get; }

    SourceControlStatusSnapshot SourceControl { get; }

    LanguageDiagnosticsSnapshot LanguageDiagnostics { get; }

    BuildDiagnosticsSnapshot BuildDiagnostics { get; }

    ProjectWorkflowSnapshot Workflow { get; }

    TestResultsSnapshot TestResults { get; }

    DebugSessionSnapshot DebugSession { get; }

    ProjectContext ProjectContext { get; }
}

/// <summary>
/// Live snapshot reader that consumes contract-level services only.
/// </summary>
internal sealed class LiveAgentContextSnapshotSources : IAgentContextSnapshotSources
{
    private readonly IEditorStateSnapshotService _editorStateSnapshotService;
    private readonly ISourceControlSnapshotService _sourceControlSnapshotService;
    private readonly ILanguageDiagnosticsService _languageDiagnosticsService;
    private readonly IBuildDiagnosticsService _buildDiagnosticsService;
    private readonly IProjectWorkflowService _projectWorkflowService;
    private readonly ITestResultsService _testResultsService;
    private readonly IDebugSessionService _debugSessionService;
    private readonly IProjectContextService _projectContextService;

    public LiveAgentContextSnapshotSources(
        IEditorStateSnapshotService editorStateSnapshotService,
        ISourceControlSnapshotService sourceControlSnapshotService,
        ILanguageDiagnosticsService languageDiagnosticsService,
        IBuildDiagnosticsService buildDiagnosticsService,
        IProjectWorkflowService projectWorkflowService,
        ITestResultsService testResultsService,
        IDebugSessionService debugSessionService,
        IProjectContextService projectContextService)
    {
        _editorStateSnapshotService = editorStateSnapshotService;
        _sourceControlSnapshotService = sourceControlSnapshotService;
        _languageDiagnosticsService = languageDiagnosticsService;
        _buildDiagnosticsService = buildDiagnosticsService;
        _projectWorkflowService = projectWorkflowService;
        _testResultsService = testResultsService;
        _debugSessionService = debugSessionService;
        _projectContextService = projectContextService;
    }

    public EditorStateSnapshot Editor => _editorStateSnapshotService.Current;

    public SourceControlStatusSnapshot SourceControl => _sourceControlSnapshotService.Current;

    public LanguageDiagnosticsSnapshot LanguageDiagnostics => _languageDiagnosticsService.Current;

    public BuildDiagnosticsSnapshot BuildDiagnostics => _buildDiagnosticsService.Current;

    public ProjectWorkflowSnapshot Workflow => _projectWorkflowService.Current;

    public TestResultsSnapshot TestResults => _testResultsService.Current;

    public DebugSessionSnapshot DebugSession => _debugSessionService.Current;

    public ProjectContext ProjectContext => _projectContextService.Current;
}
