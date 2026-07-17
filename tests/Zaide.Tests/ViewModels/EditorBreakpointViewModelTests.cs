using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.ViewModels;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Editor.Infrastructure;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.Debugging.Contracts;
using Zaide.Features.Debugging.Application;

namespace Zaide.Tests.ViewModels;

/// <summary>
/// Phase 12 M3b tests for editor breakpoint projection and DAP replacement sync.
/// </summary>
public sealed class EditorBreakpointViewModelTests
{
    static EditorBreakpointViewModelTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    [Fact]
    public async Task ToggleAtLine_AddsAndProjectsEnabledMarker()
    {
        var harness = CreateHarness();
        var source = Path.GetFullPath("/tmp/workspace/Program.cs");
        harness.Context.Set(MakeContext("/tmp/workspace"));
        harness.Tab.FilePath = source;
        harness.Tab.TextContent = "a\nb\nc\n";

        harness.Breakpoints
            .Setup(s => s.GetBreakpoints())
            .Returns(new[] { new PersistedBreakpoint(source, 2, true) });
        harness.Breakpoints
            .Setup(s => s.ToggleAsync(source, 2, default))
            .ReturnsAsync(new BreakpointOperationResult(true, null, null));

        harness.ViewModel.Activate();
        await harness.ViewModel.ToggleAtLineCommand.Execute(2).FirstAsync();

        Assert.Equal(source, harness.ViewModel.ActiveDocumentPath);
        Assert.Single(harness.ViewModel.Markers);
        Assert.Equal(2, harness.ViewModel.Markers[0].Line);
        Assert.True(harness.ViewModel.Markers[0].Enabled);
        harness.ViewModel.Dispose();
    }

    [Fact]
    public async Task ToggleAtLine_DisabledMarker_ProjectsHollowState()
    {
        var harness = CreateHarness();
        var source = Path.GetFullPath("/tmp/workspace/Program.cs");
        harness.Context.Set(MakeContext("/tmp/workspace"));
        harness.Tab.FilePath = source;

        harness.Breakpoints
            .Setup(s => s.ToggleAsync(source, 4, default))
            .ReturnsAsync(new BreakpointOperationResult(true, null, null));
        harness.Breakpoints
            .Setup(s => s.GetBreakpoints())
            .Returns(new[] { new PersistedBreakpoint(source, 4, false) });

        harness.ViewModel.Activate();
        await harness.ViewModel.ToggleAtLineCommand.Execute(4).FirstAsync();

        Assert.Single(harness.ViewModel.Markers);
        Assert.False(harness.ViewModel.Markers[0].Enabled);
        harness.ViewModel.Dispose();
    }

    [Fact]
    public async Task ActiveSessionRunning_SendsCompleteReplacementMap()
    {
        var harness = CreateHarness(DebugSessionState.Running);
        var source = Path.GetFullPath("/tmp/workspace/Program.cs");
        harness.Context.Set(MakeContext("/tmp/workspace"));
        harness.Tab.FilePath = source;
        harness.Tab.TextContent = "one\ntwo\nthree\nfour\nfive\nsix\n";

        var replacement = new Dictionary<string, IReadOnlyList<int>>
        {
            [source] = Array.Empty<int>(),
        };

        harness.Breakpoints
            .Setup(s => s.ToggleAsync(source, 5, default))
            .ReturnsAsync(new BreakpointOperationResult(true, null, null));
        harness.Breakpoints
            .Setup(s => s.GetBreakpoints())
            .Returns(Array.Empty<PersistedBreakpoint>());
        harness.Breakpoints
            .Setup(s => s.MapToDapReplacementBySource(It.Is<IReadOnlyCollection<string>>(paths =>
                paths.Single() == source)))
            .Returns(replacement);

        harness.ViewModel.Activate();
        await harness.ViewModel.ToggleAtLineCommand.Execute(5).FirstAsync();

        harness.Debug.Verify(
            s => s.ReplaceBreakpointsBySourceAsync(replacement, default),
            Times.Once);
        harness.ViewModel.Dispose();
    }

    [Fact]
    public async Task NoActiveSession_DoesNotCallDapReplacement()
    {
        var harness = CreateHarness(DebugSessionState.Idle);
        var source = Path.GetFullPath("/tmp/workspace/Program.cs");
        harness.Context.Set(MakeContext("/tmp/workspace"));
        harness.Tab.FilePath = source;

        harness.Breakpoints
            .Setup(s => s.ToggleAsync(source, 3, default))
            .ReturnsAsync(new BreakpointOperationResult(true, null, null));
        harness.Breakpoints
            .Setup(s => s.GetBreakpoints())
            .Returns(Array.Empty<PersistedBreakpoint>());

        harness.ViewModel.Activate();
        await harness.ViewModel.ToggleAtLineCommand.Execute(3).FirstAsync();

        harness.Debug.Verify(
            s => s.ReplaceBreakpointsBySourceAsync(
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<int>>>(),
                default),
            Times.Never);
        harness.ViewModel.Dispose();
    }

    [Fact]
    public void TabSwitch_UpdatesProjectionForNormalizedPath()
    {
        var harness = CreateHarness();
        var workspace = Path.GetFullPath("/tmp/workspace");
        var sourceA = Path.Combine(workspace, "A.cs");
        var sourceB = Path.Combine(workspace, "B.cs");
        harness.Context.Set(MakeContext(workspace));

        var tabA = new EditorViewModel(new Document(sourceA, "a"), new FileService());
        var tabB = new EditorViewModel(new Document(sourceB, "b"), new FileService());
        harness.EditorTabs.OpenTabs.Add(tabA);
        harness.EditorTabs.OpenTabs.Add(tabB);
        harness.EditorTabs.ActiveTab = tabA;

        harness.Breakpoints
            .Setup(s => s.GetBreakpoints())
            .Returns(new[]
            {
                new PersistedBreakpoint(Path.GetFullPath(sourceA), 1, true),
                new PersistedBreakpoint(Path.GetFullPath(sourceB), 9, true),
            });

        harness.ViewModel.Activate();
        Assert.Single(harness.ViewModel.Markers);
        Assert.Equal(1, harness.ViewModel.Markers[0].Line);

        harness.EditorTabs.ActiveTab = tabB;
        Assert.Single(harness.ViewModel.Markers);
        Assert.Equal(9, harness.ViewModel.Markers[0].Line);
        harness.ViewModel.Dispose();
    }

    [Fact]
    public void CloseActiveTab_ClearsProjection()
    {
        var harness = CreateHarness();
        harness.Context.Set(MakeContext("/tmp/workspace"));
        harness.Tab.FilePath = Path.GetFullPath("/tmp/workspace/Program.cs");
        harness.Breakpoints
            .Setup(s => s.GetBreakpoints())
            .Returns(new[]
            {
                new PersistedBreakpoint(harness.Tab.FilePath, 2, true),
            });

        harness.ViewModel.Activate();
        Assert.Single(harness.ViewModel.Markers);

        harness.EditorTabs.ActiveTab = null;
        Assert.Empty(harness.ViewModel.Markers);
        Assert.Null(harness.ViewModel.ActiveDocumentPath);
        harness.ViewModel.Dispose();
    }

    [Fact]
    public async Task ToggleAtLine_InvalidLine_IsNoOp()
    {
        var harness = CreateHarness();
        harness.Context.Set(MakeContext("/tmp/workspace"));
        harness.Tab.FilePath = Path.GetFullPath("/tmp/workspace/Program.cs");
        harness.Tab.TextContent = "only";

        harness.ViewModel.Activate();
        await harness.ViewModel.ToggleAtLineCommand.Execute(99).FirstAsync();

        harness.Breakpoints.Verify(
            s => s.ToggleAsync(It.IsAny<string>(), It.IsAny<int>(), default),
            Times.Never);
        harness.ViewModel.Dispose();
    }

    private static ProjectContext MakeContext(string? workspaceRoot) => new(
        workspaceRoot is null ? ProjectContextState.Unloaded : ProjectContextState.NoProject,
        workspaceRoot is null ? null : Path.GetFullPath(workspaceRoot),
        Array.Empty<ProjectCandidate>(),
        null,
        Array.Empty<string>(),
        null);

    private sealed record Harness(
        EditorBreakpointViewModel ViewModel,
        EditorTabViewModel EditorTabs,
        EditorViewModel Tab,
        FakeProjectContext Context,
        Mock<IBreakpointService> Breakpoints,
        Mock<IDebugSessionService> Debug);

    private static Harness CreateHarness(DebugSessionState debugState = DebugSessionState.Idle)
    {
        var workspace = new Workspace();
        var sp = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddSingleton<IFileService>(new FileService())
            .AddSingleton(workspace)
            .BuildServiceProvider();
        var editorTabs = new EditorTabViewModel(sp, new FileService(), workspace);
        var tab = new EditorViewModel(new Document("/tmp/workspace/Program.cs", ""), new FileService());
        editorTabs.OpenTabs.Add(tab);
        editorTabs.ActiveTab = tab;

        var context = new FakeProjectContext();
        var breakpoints = new Mock<IBreakpointService>();
        breakpoints.Setup(s => s.GetBreakpoints()).Returns(Array.Empty<PersistedBreakpoint>());
        breakpoints
            .Setup(s => s.MapToDapReplacementBySource(It.IsAny<IReadOnlyCollection<string>>()))
            .Returns(new Dictionary<string, IReadOnlyList<int>>());

        var debug = new Mock<IDebugSessionService>();
        debug.SetupGet(s => s.Current).Returns(new DebugSessionSnapshot(
            debugState,
            Generation: 1,
            ProgramPath: null,
            WorkingDirectory: null,
            AdapterProcessId: 1,
            StopInfo: null,
            Failure: null,
            LastOutcome: null,
            DiagnosticOutput: Array.Empty<string>(),
            BreakpointVerifications: DebugSessionSnapshot.EmptyVerifications));
        debug.SetupGet(s => s.WhenChanged).Returns(new Subject<DebugSessionSnapshot>());
        debug.Setup(s => s.ReplaceBreakpointsBySourceAsync(
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<int>>>(),
                default))
            .ReturnsAsync(new DebugSessionOperationResult(true, null, null));

        var settings = new Mock<ISettingsService>();
        settings.SetupGet(s => s.Current).Returns(SettingsModel.Defaults);
        settings.SetupGet(s => s.WhenChanged).Returns(new Subject<SettingsModel>());

        var vm = new EditorBreakpointViewModel(
            editorTabs,
            breakpoints.Object,
            debug.Object,
            context,
            settings.Object);

        return new Harness(vm, editorTabs, tab, context, breakpoints, debug);
    }

    private sealed class FakeProjectContext : IProjectContextService
    {
        private readonly Subject<ProjectContext> _subject = new();
        private ProjectContext _current = MakeContext(null);

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
    }
}