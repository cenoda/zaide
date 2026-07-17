using System;
using System.IO;
using Xunit;
using Zaide.App.Composition;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.ProjectSystem.Infrastructure;

namespace Zaide.Tests.Features.ProjectSystem.Infrastructure;

/// <summary>
/// Phase 11 M1 tests for workflow target resolution across all
/// <see cref="ProjectContextState"/> values and locked argv profiles.
/// </summary>
public sealed class ProjectTargetResolutionTests
{
    private static readonly string TempRoot = Path.Combine(
        Path.GetTempPath(),
        "zaide-phase11-m1-" + Guid.NewGuid().ToString("N"));

    static ProjectTargetResolutionTests()
    {
        Directory.CreateDirectory(TempRoot);
    }

    private static ProjectCandidate MakeCandidate(string fileName, ProjectKind kind)
    {
        var path = Path.GetFullPath(Path.Combine(TempRoot, fileName));
        return new ProjectCandidate(path, Path.GetFileNameWithoutExtension(path), kind);
    }

    private static ProjectContext MakeContext(
        ProjectContextState state,
        ProjectCandidate? selected,
        ProjectCandidate[]? candidates = null)
    {
        var list = candidates ?? (selected is not null ? new[] { selected } : Array.Empty<ProjectCandidate>());
        return new ProjectContext(
            state,
            WorkspaceRoot: TempRoot,
            list,
            selected,
            UnsupportedFiles: Array.Empty<string>(),
            ErrorMessage: state == ProjectContextState.Failed ? "discovery failed" : null);
    }

    [Theory]
    [InlineData(ProjectContextState.SingleProject)]
    [InlineData(ProjectContextState.Selected)]
    public void EligibleStates_ResolveBuildAndTest(ProjectContextState state)
    {
        var candidate = MakeCandidate("App.csproj", ProjectKind.CSharpProject);
        var context = MakeContext(state, candidate, new[] { candidate });

        var build = ProjectTargetResolver.Resolve(context, ProjectWorkflowOperation.Build);
        var test = ProjectTargetResolver.Resolve(context, ProjectWorkflowOperation.Test);

        Assert.True(build.IsSuccess);
        Assert.True(test.IsSuccess);
        Assert.Equal(candidate.FilePath, build.Target!.FilePath);
        Assert.Equal(Path.GetDirectoryName(candidate.FilePath), build.Target!.WorkingDirectory);
    }

    [Theory]
    [InlineData(ProjectContextState.Unloaded)]
    [InlineData(ProjectContextState.Loading)]
    [InlineData(ProjectContextState.NoProject)]
    [InlineData(ProjectContextState.Unsupported)]
    [InlineData(ProjectContextState.Ambiguous)]
    [InlineData(ProjectContextState.Failed)]
    public void IneligibleStates_RejectAllOperations(ProjectContextState state)
    {
        var candidate = MakeCandidate("Ignored.csproj", ProjectKind.CSharpProject);
        var context = MakeContext(
            state,
            state == ProjectContextState.Ambiguous ? null : candidate,
            new[] { candidate });

        foreach (var operation in new[]
                 {
                     ProjectWorkflowOperation.Build,
                     ProjectWorkflowOperation.Run,
                     ProjectWorkflowOperation.Test,
                 })
        {
            var resolution = ProjectTargetResolver.Resolve(context, operation);
            Assert.False(resolution.IsSuccess);
        }
    }

    [Fact]
    public void CSharpProject_Run_IsEligible()
    {
        var candidate = MakeCandidate("Console.csproj", ProjectKind.CSharpProject);
        var context = MakeContext(ProjectContextState.SingleProject, candidate);

        var resolution = ProjectTargetResolver.Resolve(context, ProjectWorkflowOperation.Run);

        Assert.True(resolution.IsSuccess);
        Assert.Equal(ProjectKind.CSharpProject, resolution.Target!.Kind);
    }

    [Theory]
    [InlineData(ProjectKind.Solution, "App.sln")]
    [InlineData(ProjectKind.SolutionX, "App.slnx")]
    public void SolutionKinds_Run_IsRejected(ProjectKind kind, string fileName)
    {
        var candidate = MakeCandidate(fileName, kind);
        var context = MakeContext(ProjectContextState.Selected, candidate, new[] { candidate });

        var build = ProjectTargetResolver.Resolve(context, ProjectWorkflowOperation.Build);
        var test = ProjectTargetResolver.Resolve(context, ProjectWorkflowOperation.Test);
        var run = ProjectTargetResolver.Resolve(context, ProjectWorkflowOperation.Run);

        Assert.True(build.IsSuccess);
        Assert.True(test.IsSuccess);
        Assert.False(run.IsSuccess);
    }

    [Fact]
    public void BuildProfile_UsesLockedDotnetBuildArgv()
    {
        var candidate = MakeCandidate("Build.csproj", ProjectKind.CSharpProject);
        var context = MakeContext(ProjectContextState.SingleProject, candidate);
        var target = ProjectTargetResolver.Resolve(context, ProjectWorkflowOperation.Build).Target!;

        var profile = ProjectExecutionProfileResolver.Resolve(target, ProjectWorkflowOperation.Build);

        Assert.Equal("dotnet", profile.FileName);
        Assert.Equal($"build \"{target.FilePath}\"", profile.Arguments);
        Assert.Equal(target.WorkingDirectory, profile.WorkingDirectory);
    }

    [Fact]
    public void TestProfile_UsesLockedDotnetTestArgv()
    {
        var candidate = MakeCandidate("Test.csproj", ProjectKind.CSharpProject);
        var context = MakeContext(ProjectContextState.SingleProject, candidate);
        var target = ProjectTargetResolver.Resolve(context, ProjectWorkflowOperation.Test).Target!;

        var profile = ProjectExecutionProfileResolver.Resolve(target, ProjectWorkflowOperation.Test);

        Assert.Equal("dotnet", profile.FileName);
        Assert.Equal($"test \"{target.FilePath}\"", profile.Arguments);
    }

    [Fact]
    public void RunProfile_UsesLockedDotnetRunProjectArgv()
    {
        var candidate = MakeCandidate("Run.csproj", ProjectKind.CSharpProject);
        var context = MakeContext(ProjectContextState.SingleProject, candidate);
        var target = ProjectTargetResolver.Resolve(context, ProjectWorkflowOperation.Run).Target!;

        var profile = ProjectExecutionProfileResolver.Resolve(target, ProjectWorkflowOperation.Run);

        Assert.Equal("dotnet", profile.FileName);
        Assert.Equal($"run --project \"{target.FilePath}\"", profile.Arguments);
    }

    [Fact]
    public void Resolve_NormalizesTargetPath()
    {
        var relative = Path.Combine(TempRoot, "Normalize.csproj");
        var candidate = new ProjectCandidate(
            relative,
            "Normalize",
            ProjectKind.CSharpProject);
        var context = MakeContext(ProjectContextState.SingleProject, candidate);

        var target = ProjectTargetResolver.Resolve(context, ProjectWorkflowOperation.Build).Target!;

        Assert.Equal(Path.GetFullPath(relative), target.FilePath);
    }
}
