using System;
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

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 12 M3b tests for <c>debug.toggleBreakpoint</c> registration and dispatch.
/// </summary>
public sealed class DebugToggleBreakpointCommandTests
{
    static DebugToggleBreakpointCommandTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    [Fact]
    public void Registry_ContainsSingleF9Command()
    {
        var registry = CommandRegistryFactory.Create();
        var harness = CreateHarness(registry);
        harness.ViewModel.Dispose();

        var descriptor = registry.GetById("debug.toggleBreakpoint");
        Assert.NotNull(descriptor);
        Assert.Equal("Toggle Breakpoint", descriptor!.DisplayName);
        Assert.Equal(new[] { "F9" }, descriptor.DefaultGestures);

        var settings = new Mock<ISettingsService>();
        settings.SetupGet(s => s.Current).Returns(SettingsModel.Defaults);
        var f9Bindings = registry.ResolveKeyBindings(settings.Object)
            .Where(binding => binding.Gesture == "F9")
            .ToList();

        Assert.Single(f9Bindings);
        Assert.Equal("debug.toggleBreakpoint", f9Bindings[0].CommandId);
    }

    [Fact]
    public async Task ToggleBreakpoint_WithActiveSavedDocument_TogglesAtCaretLine()
    {
        var registry = CommandRegistryFactory.Create();
        var harness = CreateHarness(registry);
        var source = Path.GetFullPath("/tmp/workspace/Program.cs");
        harness.Context.Set(MakeContext("/tmp/workspace"));
        harness.Tab.FilePath = source;
        harness.Tab.CaretLine = 2;
        harness.Tab.TextContent = "line1\nline2\nline3\n";
        harness.Breakpoints
            .Setup(s => s.ToggleAsync(source, 2, default))
            .ReturnsAsync(new BreakpointOperationResult(true, null, null));
        harness.Breakpoints
            .Setup(s => s.GetBreakpoints())
            .Returns(Array.Empty<PersistedBreakpoint>());
        harness.Breakpoints
            .Setup(s => s.MapToDapReplacementBySource(It.IsAny<System.Collections.Generic.IReadOnlyCollection<string>>()))
            .Returns(new System.Collections.Generic.Dictionary<string, System.Collections.Generic.IReadOnlyList<int>>
            {
                [source] = new[] { 12 },
            });

        harness.ViewModel.Activate();
        Assert.True(harness.ViewModel.ToggleBreakpointCommand.CanExecute.FirstAsync().Wait());

        await harness.ViewModel.ToggleBreakpointCommand.Execute();

        harness.Breakpoints.Verify(s => s.ToggleAsync(source, 2, default), Times.Once);
        harness.ViewModel.Dispose();
    }

    [Fact]
    public void ToggleBreakpoint_UntitledDocument_IsUnavailable()
    {
        var harness = CreateHarness();
        harness.Context.Set(MakeContext("/tmp/workspace"));
        harness.Tab.FilePath = string.Empty;
        harness.Tab.CaretLine = 1;
        harness.ViewModel.Activate();

        Assert.False(harness.ViewModel.ToggleBreakpointCommand.CanExecute.FirstAsync().Wait());
        harness.ViewModel.Dispose();
    }

    [Fact]
    public void ToggleBreakpoint_NoWorkspace_IsUnavailable()
    {
        var harness = CreateHarness();
        harness.Context.Set(MakeContext(null));
        harness.Tab.FilePath = "/tmp/workspace/Program.cs";
        harness.Tab.CaretLine = 1;
        harness.ViewModel.Activate();

        Assert.False(harness.ViewModel.ToggleBreakpointCommand.CanExecute.FirstAsync().Wait());
        harness.ViewModel.Dispose();
    }

    private static ProjectContext MakeContext(string? workspaceRoot) => new(
        workspaceRoot is null ? ProjectContextState.Unloaded : ProjectContextState.NoProject,
        workspaceRoot,
        Array.Empty<ProjectCandidate>(),
        null,
        Array.Empty<string>(),
        null);

    private sealed record Harness(
        EditorBreakpointViewModel ViewModel,
        EditorViewModel Tab,
        FakeProjectContext Context,
        Mock<IBreakpointService> Breakpoints);

    private static Harness CreateHarness(ICommandRegistry? registry = null)
    {
        var tab = new EditorViewModel(new Document("/tmp/workspace/Program.cs", "class App {}"), new FileService());
        var editorTabs = new EditorTabViewModel(
            new Microsoft.Extensions.DependencyInjection.ServiceCollection()
                .AddSingleton<IFileService>(new FileService())
                .AddSingleton(new Workspace())
                .BuildServiceProvider(),
            new FileService(),
            new Workspace());
        editorTabs.OpenTabs.Add(tab);
        editorTabs.ActiveTab = tab;

        var context = new FakeProjectContext();
        var breakpoints = new Mock<IBreakpointService>();
        breakpoints.Setup(s => s.GetBreakpoints()).Returns(Array.Empty<PersistedBreakpoint>());
        breakpoints
            .Setup(s => s.MapToDapReplacementBySource(It.IsAny<System.Collections.Generic.IReadOnlyCollection<string>>()))
            .Returns(new System.Collections.Generic.Dictionary<string, System.Collections.Generic.IReadOnlyList<int>>());

        var debug = TestOperationGateFactory.CreateIdleDebugSession();
        var settings = new Mock<ISettingsService>();
        settings.SetupGet(s => s.Current).Returns(SettingsModel.Defaults);
        settings.SetupGet(s => s.WhenChanged).Returns(new Subject<SettingsModel>());

        var vm = new EditorBreakpointViewModel(
            editorTabs,
            breakpoints.Object,
            debug.Object,
            context,
            settings.Object,
            registry);

        return new Harness(vm, tab, context, breakpoints);
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