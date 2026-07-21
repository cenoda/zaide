using System;
using System.IO;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ReactiveUI;
using ReactiveUI.Avalonia;
using ReactiveUI.Builder;
using Splat;
using Xunit;
using Zaide.App.Shell;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Infrastructure;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.ProjectSystem.Presentation;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Infrastructure;
using Zaide.Features.Settings.Presentation;
using Zaide.Features.SourceControl.Application;
using Zaide.Features.SourceControl.Contracts;
using Zaide.Features.SourceControl.Domain;
using Zaide.Features.SourceControl.Presentation;
using Zaide.Features.Terminal.Contracts;
using Zaide.Features.Terminal.Infrastructure;
using Zaide.Features.Terminal.Presentation;
using Zaide.Features.Townhall.Presentation;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Workspace.Infrastructure;
using Zaide.Features.Workspace.Presentation;
using Zaide.Tests.Features.Conversations;
using Zaide.Tests.Features.Debugging.Application;
using Zaide.Tests.Features.Debugging.Presentation;
using Zaide.Tests.Features.ProjectSystem;

namespace Zaide.Tests.App.Shell;

/// <summary>
/// Lifecycle proofs for <see cref="SettingsPanelAttachHost"/> (Refactor 8 M5):
/// attach/detach on open/close, left-panel restore, deactivate cleanup, and
/// editor focus restoration seam.
/// </summary>
public sealed class SettingsPanelAttachHostTests
{
    static SettingsPanelAttachHostTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
        Locator.CurrentMutable.Register(
            () => new AvaloniaActivationForViewFetcher(),
            typeof(IActivationForViewFetcher));
        EnsureApplication();
    }

    [Fact]
    public void ShowAndHide_AttachAndDetachPanelFromLayout()
    {
        using var settings = new TestSettingsService();
        using var vm = CreateMainWindowViewModel();
        var layoutRoot = new Grid();
        using var disposables = new CompositeDisposable();
        var host = CreateHost(settings, layoutRoot, vm, disposables);

        host.ShowPanel();
        var panel = host.PanelForTests;
        Assert.NotNull(panel);
        Assert.Contains(panel, layoutRoot.Children);
        Assert.True(vm.IsSettingsOpen);

        host.HidePanel();
        Assert.DoesNotContain(panel, layoutRoot.Children);
        Assert.False(vm.IsSettingsOpen);
        Assert.Same(panel, host.PanelForTests);
    }

    [Fact]
    public void ShowSettingsInteraction_TogglesOpenStateAndReusesPanel()
    {
        using var settings = new TestSettingsService();
        using var vm = CreateMainWindowViewModel();
        var layoutRoot = new Grid();
        using var disposables = new CompositeDisposable();
        var host = CreateHost(settings, layoutRoot, vm, disposables);
        var context = new TestInteractionContext();

        host.HandleShowSettings(context);
        var firstPanel = host.PanelForTests;
        Assert.True(context.Output);
        Assert.NotNull(firstPanel);
        Assert.Contains(firstPanel, layoutRoot.Children);

        context = new TestInteractionContext();
        host.HandleShowSettings(context);
        Assert.False(context.Output);
        Assert.DoesNotContain(firstPanel, layoutRoot.Children);
        Assert.Same(firstPanel, host.PanelForTests);

        context = new TestInteractionContext();
        host.HandleShowSettings(context);
        Assert.True(context.Output);
        Assert.Same(firstPanel, host.PanelForTests);
        Assert.Contains(firstPanel, layoutRoot.Children);
    }

    [Fact]
    public void HidePanel_RestoresPreviousLeftPanelMode()
    {
        using var settings = new TestSettingsService();
        using var vm = CreateMainWindowViewModel();
        var layoutRoot = new Grid();
        using var disposables = new CompositeDisposable();
        var host = CreateHost(settings, layoutRoot, vm, disposables);

        vm.LeftPanelMode = LeftPanelMode.SourceControl;
        host.ShowPanel();
        host.HidePanel();

        Assert.Equal(LeftPanelMode.SourceControl, vm.LeftPanelMode);

        vm.LeftPanelMode = LeftPanelMode.Explorer;
        host.ShowPanel();
        host.HidePanel();
        Assert.Equal(LeftPanelMode.Explorer, vm.LeftPanelMode);
    }

    [Fact]
    public void WireToViewModel_Dispose_ClosesPanelAndClearsOpenState()
    {
        using var settings = new TestSettingsService();
        using var vm = CreateMainWindowViewModel();
        var layoutRoot = new Grid();
        var disposables = new CompositeDisposable();
        var host = CreateHost(settings, layoutRoot, vm, disposables);

        host.ShowPanel();
        var subscriptionCountBeforeClose = settings.SubscriptionDisposeCount;

        disposables.Dispose();

        Assert.Null(host.PanelForTests);
        Assert.False(vm.IsSettingsOpen);
        Assert.Empty(layoutRoot.Children);
        Assert.Equal(subscriptionCountBeforeClose + 1, settings.SubscriptionDisposeCount);
    }

    [Fact]
    public void RestoreEditorFocusAfterSettings_WithNullEditor_IsNoOp()
    {
        using var vm = CreateMainWindowViewModel();
        var exception = Record.Exception(() =>
            SettingsPanelAttachHost.RestoreEditorFocusAfterSettings(vm, null));
        Assert.Null(exception);
    }

    [Fact]
    public void RestoreEditorFocusAfterSettings_WithNullViewModel_IsNoOp()
    {
        var exception = Record.Exception(() =>
            SettingsPanelAttachHost.RestoreEditorFocusAfterSettings(null, null));
        Assert.Null(exception);
    }

    [Fact]
    public void SettingsPanelAttachHost_IsInternalShellTypeWithWireToViewModel()
    {
        var type = typeof(SettingsPanelAttachHost);

        Assert.False(type.IsPublic);
        Assert.Equal("Zaide.App.Shell", type.Namespace);
        Assert.Contains(type.GetMethods(BindingFlags.Instance | BindingFlags.Public),
            m => m.Name == nameof(SettingsPanelAttachHost.WireToViewModel));
    }

    private static SettingsPanelAttachHost CreateHost(
        TestSettingsService settings,
        Grid layoutRoot,
        MainWindowViewModel vm,
        CompositeDisposable disposables)
    {
        var host = new SettingsPanelAttachHost(
            settings,
            new TestSecretStore(),
            new SettingsPanelFactory(),
            layoutRoot,
            () => null!);
        host.WireToViewModel(vm, disposables);
        return host;
    }

    private static MainWindowViewModel CreateMainWindowViewModel()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(new FileService());
        services.AddSingleton<IEditorSessionFactory, EditorSessionFactory>();
        services.AddSingleton<Workspace>();
        using var provider = services.BuildServiceProvider();
        var fileTree = new FileTreeViewModel(new FileTreeService(), CurrentThreadScheduler.Instance);
        var editorTabs = new EditorTabViewModel(
            provider.GetRequiredService<IEditorSessionFactory>(),
            provider.GetRequiredService<IFileService>(),
            provider.GetRequiredService<Workspace>());
        var terminalFactory = new Mock<ITerminalServiceFactory>();
        terminalFactory.Setup(factory => factory.Create()).Returns(new Mock<ITerminalService>().Object);
        var terminalHost = new TerminalHost(terminalFactory.Object);
        var panelHost = ConversationsTestSupport.CreatePanelHost();
        var coordinator = new Mock<IAgentExecutionCoordinator>().Object;
        var router = new AgentRouter(new MentionParser(), panelHost, coordinator, ConversationsTestSupport.CreateCatalog(), ConversationsTestSupport.CreateStore());
        var townhall = ConversationsTestSupport.CreateTownhallViewModel();
        var git = new Mock<IGitRepositoryService>();
        git.Setup(service => service.Discover(It.IsAny<string>())).Returns(RepositoryDiscoveryResult.NotFound(""));
        git.Setup(service => service.ReadStatus(It.IsAny<string>())).Returns(new RepositoryStatusSnapshot());
        var mutation = new Mock<IGitMutationService>();
        var sourceControl = new SourceControlViewModel(
            new SourceControlSnapshotOrchestrator(git.Object),
            new Workspace(),
            mutation.Object,
            git.Object);
        var ctxMock = new Mock<IProjectContextService>(MockBehavior.Loose);
        ctxMock.Setup(s => s.WhenChanged).Returns(Observable.Never<ProjectContext>());
        var workspace = provider.GetRequiredService<Workspace>();
        var vm = new MainWindowViewModel(
            fileTree,
            editorTabs,
            terminalHost, townhall,
            sourceControl,
            TestProblemsFactory.Create(workspace, editorTabs),
            TestProjectWorkflowFactory.Create(),
            TestTestResultsFactory.Create(),
            TestDebugSessionFactory.Create(),
            TestDebugPanelFactory.Create(),
            TestEditorBreakpointFactory.Create(editorTabs),
            workspace,
            ctxMock.Object,
            ConversationsTestSupport.CreateCatalogAsInterface());
        vm.Activate();
        return vm;
    }

    private static void EnsureApplication()
    {
        if (Application.Current is global::Zaide.App.Composition.App app)
        {
            if (!app.Resources.ContainsKey("PrimaryAccentBrush"))
                app.Initialize();
            return;
        }

        new global::Zaide.App.Composition.App().Initialize();
    }

    private sealed class TestSecretStore : ISecretStore
    {
        public string? Get(string key) => null;
        public void Set(string key, string value) { }
        public void Delete(string key) { }
    }

    private sealed class TestSettingsService : ISettingsService, IDisposable
    {
        private readonly Subject<SettingsModel> _changes = new();
        public SettingsModel Current { get; private set; } = SettingsModel.Defaults;
        public IObservable<SettingsModel> WhenChanged => new CountingObservable(_changes, this);
        public SettingsLoadResult LoadResult => SettingsLoadResult.Missing;
        public IObservable<SettingsSaveError> WriteErrors => Observable.Empty<SettingsSaveError>();
        public int SubscriptionDisposeCount { get; private set; }

        public Task<SettingsMutationResult> ApplyAsync(
            SettingsModel expectedCurrent,
            SettingsModel next,
            CancellationToken ct = default) =>
            Task.FromResult<SettingsMutationResult>(
                new SettingsMutationResult.Applied(next, new SettingsSaveResult.Saved()));

        public Task<SettingsMutationResult> UpdateAsync(
            Func<SettingsModel, SettingsModel> producer,
            CancellationToken ct = default) =>
            ApplyAsync(Current, producer(Current), ct);

        public Task<SettingsSaveResult> SaveAsync(CancellationToken ct = default) =>
            Task.FromResult<SettingsSaveResult>(new SettingsSaveResult.Saved());

        public void Dispose() => _changes.Dispose();

        private sealed class CountingObservable(Subject<SettingsModel> source, TestSettingsService owner)
            : IObservable<SettingsModel>
        {
            public IDisposable Subscribe(IObserver<SettingsModel> observer) =>
                new CountingSubscription(source.Subscribe(observer), owner);
        }

        private sealed class CountingSubscription(IDisposable inner, TestSettingsService owner) : IDisposable
        {
            private int _disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    owner.SubscriptionDisposeCount++;
                    inner.Dispose();
                }
            }
        }
    }

    private sealed class TestInteractionContext : IInteractionContext<Unit, bool>
    {
        public Unit Input => Unit.Default;
        public bool Output { get; private set; }
        public bool IsHandled { get; private set; }
        public void SetOutput(bool output)
        {
            Output = output;
            IsHandled = true;
        }
    }
}
