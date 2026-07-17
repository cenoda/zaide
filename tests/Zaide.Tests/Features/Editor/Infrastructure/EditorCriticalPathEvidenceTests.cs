using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Zaide.App.Composition;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.ProjectSystem.Infrastructure;

namespace Zaide.Tests.Features.Editor.Infrastructure;

/// <summary>
/// Phase 13 M4a: bounded automated critical-path evidence for the workflow-console
/// fixture. This class fills the real headless gap for "open selected C# project"
/// via production discovery + project-context load. Other critical-path steps are
/// composed from existing Phase 10–12 / M0 focused proofs and recorded in
/// <c>docs/phases/v2/phase-13/M4A_CRITICAL_PATH_EVIDENCE.md</c>; this file does
/// not fake an end-to-end path by calling unrelated test methods.
/// </summary>
public sealed class EditorCriticalPathEvidenceTests
{
    private static readonly string FixtureRoot = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "fixtures", "workflow-console"));

    private static readonly string FixtureProject = Path.Combine(FixtureRoot, "WorkflowConsole.csproj");

    private static readonly string FixtureProgram = Path.Combine(FixtureRoot, "Program.cs");

    [Fact]
    public async Task OpenSelectedCSharpProject_WorkflowConsole_LoadsSingleProjectContext()
    {
        Assert.True(Directory.Exists(FixtureRoot), $"Missing fixture root: {FixtureRoot}");
        Assert.True(File.Exists(FixtureProject), $"Missing fixture project: {FixtureProject}");

        var discovery = new ProjectDiscovery(new FileSystemProjectFileSystem());
        using var service = new ProjectContextService(
            discovery,
            NullLogger<ProjectContextService>.Instance);

        await service.LoadAsync(FixtureRoot);

        var current = service.Current;
        Assert.Equal(ProjectContextState.SingleProject, current.State);
        Assert.Equal(Path.GetFullPath(FixtureRoot), current.WorkspaceRoot);
        Assert.NotNull(current.SelectedProject);
        Assert.Equal(Path.GetFullPath(FixtureProject), current.SelectedProject!.FilePath);
        Assert.Equal("WorkflowConsole", current.SelectedProject.DisplayName);
        Assert.Equal(ProjectKind.CSharpProject, current.SelectedProject.Kind);
        Assert.Single(current.Candidates);
        Assert.Empty(current.UnsupportedFiles);
        Assert.Null(current.ErrorMessage);
    }

    [Fact]
    public async Task EditAndSave_WorkflowConsole_OpenEditSaveRestore_Passes()
    {
        Assert.True(File.Exists(FixtureProgram), $"Missing fixture source: {FixtureProgram}");

        var sourceShaBefore = EditorMeasurementSeam.Sha256Hex(FixtureProgram);
        var sample = await EditorMeasurementSeam
            .MeasureOpenEditSaveRestoreAsync(FixtureProgram, sampleNumber: 1);

        Assert.Equal("pass", sample.Status);
        Assert.True(sample.Restored, sample.Note);
        Assert.Equal(sourceShaBefore, sample.FixtureSha256);
        Assert.Equal(sourceShaBefore, EditorMeasurementSeam.Sha256Hex(FixtureProgram));
    }
}
