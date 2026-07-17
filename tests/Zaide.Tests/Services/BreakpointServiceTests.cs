using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Infrastructure;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 12 M2 tests for workspace-scoped breakpoint persistence and DAP mapping.
/// </summary>
public sealed class BreakpointServiceTests : IDisposable
{
    private static readonly string TempRoot = Path.Combine(
        Path.GetTempPath(),
        "zaide-phase12-m2-breakpoints-" + Guid.NewGuid().ToString("N"));

    private readonly string _tempDir;
    private readonly string _settingsPath;
    private readonly string _lastKnownGoodPath;
    private readonly string _tempPath;
    private readonly string _workspaceA;
    private readonly string _workspaceB;

    public BreakpointServiceTests()
    {
        Directory.CreateDirectory(TempRoot);
        _tempDir = Path.Combine(TempRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "settings.json");
        _lastKnownGoodPath = Path.Combine(_tempDir, "settings.json.lastknowngood");
        _tempPath = Path.Combine(_tempDir, "settings.json.tmp");
        _workspaceA = Path.Combine(_tempDir, "workspace-a");
        _workspaceB = Path.Combine(_tempDir, "workspace-b");
        Directory.CreateDirectory(_workspaceA);
        Directory.CreateDirectory(_workspaceB);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    private sealed class FakeProjectContextService : IProjectContextService
    {
        private readonly Subject<ProjectContext> _subject = new();
        private ProjectContext _current = Unloaded();

        public ProjectContext Current => _current;

        public IObservable<ProjectContext> WhenChanged => _subject;

        public void Set(ProjectContext context)
        {
            _current = context;
            _subject.OnNext(context);
        }

        public Task LoadAsync(string workspaceRoot, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task ReloadAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UnloadAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void SelectProject(ProjectCandidate? candidate) =>
            throw new NotSupportedException();

        public void Dispose()
        {
            _subject.OnCompleted();
            _subject.Dispose();
        }

        private static ProjectContext Unloaded() => new(
            ProjectContextState.Unloaded,
            WorkspaceRoot: null,
            Candidates: Array.Empty<ProjectCandidate>(),
            SelectedProject: null,
            UnsupportedFiles: Array.Empty<string>(),
            ErrorMessage: null);
    }

    private SettingsService CreateSettingsService()
    {
        File.WriteAllText(_settingsPath, SettingsSerializer.Serialize(SettingsModel.Defaults));
        return new SettingsService(
            _settingsPath,
            _lastKnownGoodPath,
            _tempPath,
            new SettingsMigrator(new ISettingsMigration[]
            {
                new SettingsMigrationV1ToV2(),
                new SettingsMigrationV2ToV3(),
            }));
    }

    private static ProjectContext MakeContext(string? workspaceRoot) => new(
        workspaceRoot is null ? ProjectContextState.Unloaded : ProjectContextState.NoProject,
        workspaceRoot,
        Candidates: Array.Empty<ProjectCandidate>(),
        SelectedProject: null,
        UnsupportedFiles: Array.Empty<string>(),
        ErrorMessage: null);

    private static string SourcePath(string workspaceRoot, string fileName) =>
        Path.GetFullPath(Path.Combine(workspaceRoot, fileName));

    [Fact]
    public async Task AddAsync_NoWorkspace_DoesNotPersist()
    {
        using var settings = CreateSettingsService();
        using var context = new FakeProjectContextService();
        var service = new BreakpointService(context, settings);
        var source = SourcePath(_workspaceA, "Program.cs");

        var result = await service.AddAsync(source, 10);

        Assert.False(result.Succeeded);
        Assert.Equal(BreakpointOutcomeKind.NoWorkspace, result.Outcome);
        Assert.Empty(service.GetBreakpoints());
        Assert.Empty(settings.Current.Debug.BreakpointsByWorkspaceRoot);
    }

    [Fact]
    public async Task AddAsync_InvalidLine_IsRejected()
    {
        using var settings = CreateSettingsService();
        using var context = new FakeProjectContextService();
        context.Set(MakeContext(_workspaceA));
        var service = new BreakpointService(context, settings);

        var result = await service.AddAsync(SourcePath(_workspaceA, "Program.cs"), 0);

        Assert.False(result.Succeeded);
        Assert.Equal(BreakpointOutcomeKind.InvalidLine, result.Outcome);
        Assert.Empty(service.GetBreakpoints());
    }

    [Fact]
    public async Task AddAsync_NormalizesPaths_AndPersistsEnabledBreakpoint()
    {
        using var settings = CreateSettingsService();
        using var context = new FakeProjectContextService();
        context.Set(MakeContext(_workspaceA));
        var service = new BreakpointService(context, settings);
        var relativeSource = Path.Combine(_workspaceA, "Program.cs");
        var expectedSource = Path.GetFullPath(relativeSource);
        var expectedRoot = Path.GetFullPath(_workspaceA);

        var result = await service.AddAsync(relativeSource, 12);

        Assert.True(result.Succeeded);
        var snapshot = service.GetBreakpoints();
        Assert.Single(snapshot);
        Assert.Equal(expectedSource, snapshot[0].SourcePath);
        Assert.Equal(12, snapshot[0].Line);
        Assert.True(snapshot[0].Enabled);

        var persisted = settings.Current.Debug.BreakpointsByWorkspaceRoot[expectedRoot];
        Assert.Single(persisted);
        Assert.Equal(expectedSource, persisted[0].SourcePath);
    }

    [Fact]
    public async Task WorkspaceIsolation_BreakpointsAreScopedByRoot()
    {
        using var settings = CreateSettingsService();
        using var context = new FakeProjectContextService();
        var service = new BreakpointService(context, settings);
        var sourceA = SourcePath(_workspaceA, "A.cs");
        var sourceB = SourcePath(_workspaceB, "B.cs");

        context.Set(MakeContext(_workspaceA));
        Assert.True((await service.AddAsync(sourceA, 1)).Succeeded);

        context.Set(MakeContext(_workspaceB));
        Assert.True((await service.AddAsync(sourceB, 2)).Succeeded);
        Assert.Single(service.GetBreakpoints());
        Assert.Equal(sourceB, service.GetBreakpoints()[0].SourcePath);

        context.Set(MakeContext(_workspaceA));
        var workspaceABreakpoints = service.GetBreakpoints();
        Assert.Single(workspaceABreakpoints);
        Assert.Equal(sourceA, workspaceABreakpoints[0].SourcePath);
        Assert.Equal(1, workspaceABreakpoints[0].Line);
    }

    [Fact]
    public async Task RemoveAsync_RemovesMatchingBreakpoint()
    {
        using var settings = CreateSettingsService();
        using var context = new FakeProjectContextService();
        context.Set(MakeContext(_workspaceA));
        var service = new BreakpointService(context, settings);
        var source = SourcePath(_workspaceA, "Program.cs");

        Assert.True((await service.AddAsync(source, 5)).Succeeded);
        Assert.True((await service.RemoveAsync(source, 5)).Succeeded);
        Assert.Empty(service.GetBreakpoints());
    }

    [Fact]
    public async Task RemoveAsync_NotFound_IsRejected()
    {
        using var settings = CreateSettingsService();
        using var context = new FakeProjectContextService();
        context.Set(MakeContext(_workspaceA));
        var service = new BreakpointService(context, settings);

        var result = await service.RemoveAsync(SourcePath(_workspaceA, "Program.cs"), 3);

        Assert.False(result.Succeeded);
        Assert.Equal(BreakpointOutcomeKind.NotFound, result.Outcome);
    }

    [Fact]
    public async Task ToggleAsync_FlipsEnabledState_AndAddsWhenMissing()
    {
        using var settings = CreateSettingsService();
        using var context = new FakeProjectContextService();
        context.Set(MakeContext(_workspaceA));
        var service = new BreakpointService(context, settings);
        var source = SourcePath(_workspaceA, "Program.cs");

        Assert.True((await service.ToggleAsync(source, 7)).Succeeded);
        Assert.True(service.GetBreakpoints()[0].Enabled);

        Assert.True((await service.ToggleAsync(source, 7)).Succeeded);
        Assert.False(service.GetBreakpoints()[0].Enabled);

        Assert.True((await service.ToggleAsync(source, 7)).Succeeded);
        Assert.True(service.GetBreakpoints()[0].Enabled);
    }

    [Fact]
    public async Task GetBreakpoints_ReturnsDefensiveCopy()
    {
        using var settings = CreateSettingsService();
        using var context = new FakeProjectContextService();
        context.Set(MakeContext(_workspaceA));
        var service = new BreakpointService(context, settings);
        var source = SourcePath(_workspaceA, "Program.cs");

        await service.AddAsync(source, 4);

        var first = service.GetBreakpoints();
        var second = service.GetBreakpoints();

        Assert.NotSame(first, second);
        Assert.Equal(first[0].SourcePath, second[0].SourcePath);
    }

    [Fact]
    public async Task MapToDapReplacementBySource_IncludesEmptySetsForRequestedSources()
    {
        using var settings = CreateSettingsService();
        using var context = new FakeProjectContextService();
        context.Set(MakeContext(_workspaceA));
        var service = new BreakpointService(context, settings);
        var withBreakpoint = SourcePath(_workspaceA, "Hit.cs");
        var withoutBreakpoint = SourcePath(_workspaceA, "Miss.cs");

        Assert.True((await service.AddAsync(withBreakpoint, 9)).Succeeded);
        Assert.True((await service.AddAsync(withBreakpoint, 3)).Succeeded);
        Assert.True((await service.ToggleAsync(withBreakpoint, 3)).Succeeded);

        var mapping = service.MapToDapReplacementBySource(new[]
        {
            withBreakpoint,
            withoutBreakpoint,
        });

        Assert.Equal(2, mapping.Count);
        Assert.Equal(new[] { 9 }, mapping[Path.GetFullPath(withBreakpoint)]);
        Assert.Empty(mapping[Path.GetFullPath(withoutBreakpoint)]);
        Assert.IsType<ReadOnlyDictionary<string, IReadOnlyList<int>>>(mapping);
    }

    [Fact]
    public async Task MapToDapReplacementBySource_NormalizesRequestedSourcePaths()
    {
        using var settings = CreateSettingsService();
        using var context = new FakeProjectContextService();
        context.Set(MakeContext(_workspaceA));
        var service = new BreakpointService(context, settings);
        var source = SourcePath(_workspaceA, "Program.cs");

        Assert.True((await service.AddAsync(source, 11)).Succeeded);

        var mapping = service.MapToDapReplacementBySource(new[]
        {
            Path.Combine(_workspaceA, "Program.cs"),
        });

        Assert.Single(mapping);
        Assert.Equal(new[] { 11 }, mapping[Path.GetFullPath(source)]);
    }
}