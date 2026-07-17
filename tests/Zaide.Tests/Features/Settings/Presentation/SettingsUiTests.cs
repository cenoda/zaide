using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Reactive.Subjects;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Infrastructure;
using Zaide.Features.Editor.Presentation;

using Microsoft.Extensions.DependencyInjection;
using Moq;
using ReactiveUI;
using ReactiveUI.Avalonia;
using ReactiveUI.Builder;
using Splat;
using Xunit;
using Zaide;
using Zaide.Models;
using Zaide.Services;
using Zaide.ViewModels;
using Zaide.Views;

using Zaide.Tests;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Presentation;
using Zaide.Features.Settings.Infrastructure;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Workspace.Infrastructure;
using Zaide.Features.Workspace.Presentation;

namespace Zaide.Tests.Features.Settings.Presentation;

public sealed class SettingsUiTests
{
    static SettingsUiTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
        Locator.CurrentMutable.Register(() => new AvaloniaActivationForViewFetcher(), typeof(IActivationForViewFetcher));
        EnsureApplication();
    }

    [Fact]
    public async Task GearCommand_TogglesSettingsOpenState_AndReusesSinglePanelInstance()
    {
        using var settings = new TestSettingsService();
        var secrets = new TestSecretStore();
        using var vm = CreateMainWindowViewModel();
        using var status = new StatusBarViewModel(vm, settings, new EmptyLanguageSessionService(), ImmediateScheduler.Instance);
        var window = CreateSettingsTestWindow(settings, secrets, vm);

        await status.OpenSettingsCommand.Execute().ToTask();
        var firstPanel = GetSettingsPanel(window);
        Assert.NotNull(firstPanel);
        Assert.True(vm.IsSettingsOpen);
        Assert.True(status.IsSettingsOpen);

        await status.OpenSettingsCommand.Execute().ToTask();
        Assert.False(vm.IsSettingsOpen);
        Assert.False(status.IsSettingsOpen);
        Assert.Same(firstPanel, GetSettingsPanel(window));

        await status.OpenSettingsCommand.Execute().ToTask();
        Assert.True(vm.IsSettingsOpen);
        Assert.Same(firstPanel, GetSettingsPanel(window));
    }

    [Fact]
    public async Task GearCommand_RestoresPreviousLeftPanelMode_WithExplorerFallback()
    {
        using var settings = new TestSettingsService();
        var secrets = new TestSecretStore();
        using var vm = CreateMainWindowViewModel();
        using var status = new StatusBarViewModel(vm, settings, new EmptyLanguageSessionService(), ImmediateScheduler.Instance);
        var window = CreateSettingsTestWindow(settings, secrets, vm);

        vm.LeftPanelMode = LeftPanelMode.SourceControl;
        await status.OpenSettingsCommand.Execute().ToTask();
        Assert.True(vm.IsSettingsOpen);

        await status.OpenSettingsCommand.Execute().ToTask();
        Assert.False(vm.IsSettingsOpen);
        Assert.Equal(LeftPanelMode.SourceControl, vm.LeftPanelMode);

        vm.LeftPanelMode = LeftPanelMode.Explorer;
        await status.OpenSettingsCommand.Execute().ToTask();
        await status.OpenSettingsCommand.Execute().ToTask();
        Assert.Equal(LeftPanelMode.Explorer, vm.LeftPanelMode);
    }

    [Fact]
    public async Task GearCommand_WindowDeactivate_DisposesSettingsPanelOnce()
    {
        using var settings = new TestSettingsService();
        var secrets = new TestSecretStore();
        using var vm = CreateMainWindowViewModel();
        using var status = new StatusBarViewModel(vm, settings, new EmptyLanguageSessionService(), ImmediateScheduler.Instance);
        var window = CreateSettingsTestWindow(settings, secrets, vm);

        await status.OpenSettingsCommand.Execute().ToTask();
        var subscriptionCountBeforeClose = settings.SubscriptionDisposeCount;

        typeof(MainWindow).GetMethod("CloseSettingsPanel", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(window, null);

        Assert.Null(GetSettingsPanel(window));
        Assert.False(vm.IsSettingsOpen);
        Assert.Equal(subscriptionCountBeforeClose + 1, settings.SubscriptionDisposeCount);
    }

    [Fact]
    public void SettingsChanges_AreDeliveredThroughInjectedScheduler_ToStatusAndPanelProjection()
    {
        using var settings = new TestSettingsService();
        using var vm = CreateMainWindowViewModel();
        var scheduler = new RecordingScheduler();
        using var status = new StatusBarViewModel(vm, settings, new EmptyLanguageSessionService(), scheduler);
        using var panelVm = new SettingsViewModel(settings, new TestSecretStore(), scheduler);

        var next = settings.Current with { Llm = settings.Current.Llm with { Model = "scheduler-model" } };
        settings.Publish(next);

        Assert.True(scheduler.ScheduleCount > 0);
        Assert.Equal("scheduler-model", status.ConfiguredModel);
        Assert.Same(next, panelVm.ConflictSnapshot);
    }

    private static SettingsPanelView? GetSettingsPanel(MainWindow window) =>
        (SettingsPanelView?)typeof(MainWindow).GetField("_settingsPanel", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(window);

    private static void SetField<T>(MainWindow window, string name, T value) =>
        typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, value);

    private static MainWindow CreateSettingsTestWindow(
        TestSettingsService settings,
        TestSecretStore secrets,
        MainWindowViewModel vm)
    {
        var window = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
        SetField(window, "_settings", settings);
        SetField(window, "_secrets", secrets);
        SetField(window, "_layoutRoot", new Grid());
        SetField(window, "_settingsLifecycleViewModel", vm);
        var handler = typeof(MainWindow).GetMethod("HandleShowSettingsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        vm.ShowSettings.RegisterHandler(async context =>
        {
            var task = (Task)handler.Invoke(window, new object[] { context })!;
            await task;
        });
        return window;
    }

    private static MainWindowViewModel CreateMainWindowViewModel()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFileService>(new FileService());
        services.AddTransient<EditorViewModel>();
        services.AddSingleton<global::Zaide.Features.Workspace.Domain.Workspace>();
        using var provider = services.BuildServiceProvider();
        var fileTree = new FileTreeViewModel(new FileTreeService(), CurrentThreadScheduler.Instance);
        var editorTabs = new EditorTabViewModel(provider, provider.GetRequiredService<IFileService>(), provider.GetRequiredService<global::Zaide.Features.Workspace.Domain.Workspace>());
        var terminalService = new Mock<ITerminalService>();
        var terminalVm = new TerminalViewModel(terminalService.Object, action => action());
        var terminalFactory = new Mock<ITerminalSessionFactory>();
        terminalFactory.Setup(factory => factory.CreateSession()).Returns(terminalVm);
        var terminalHost = new TerminalHost(terminalFactory.Object);
        var panelHost = new AgentPanelHost();
        var coordinator = new Mock<IAgentExecutionCoordinator>().Object;
        var router = new AgentRouter(new MentionParser(panelHost), panelHost, coordinator);
        var townhall = new TownhallViewModel(new TownhallState());
        var git = new Mock<IGitRepositoryService>();
        git.Setup(service => service.Discover(It.IsAny<string>())).Returns(RepositoryDiscoveryResult.NotFound(""));
        git.Setup(service => service.ReadStatus(It.IsAny<string>())).Returns(new RepositoryStatusSnapshot());
        var mutation = new Mock<IGitMutationService>();
        var sourceControl = new SourceControlViewModel(
            new SourceControlSnapshotOrchestrator(git.Object),
            new global::Zaide.Features.Workspace.Domain.Workspace(), mutation.Object, git.Object);
        var ctxMock = new Mock<IProjectContextService>(MockBehavior.Loose);
        ctxMock.Setup(s => s.WhenChanged).Returns(Observable.Never<ProjectContext>());
        var workspace = provider.GetRequiredService<global::Zaide.Features.Workspace.Domain.Workspace>();
        var vm = new MainWindowViewModel(fileTree, editorTabs, terminalHost, panelHost, coordinator, router, townhall,
            sourceControl, TestProblemsFactory.Create(workspace, editorTabs), TestProjectWorkflowFactory.Create(), TestTestResultsFactory.Create(), TestDebugSessionFactory.Create(), TestDebugPanelFactory.Create(), TestEditorBreakpointFactory.Create(editorTabs), workspace, ctxMock.Object);
        vm.Activate();
        return vm;
    }

    private static void EnsureApplication()
    {
        if (Application.Current is App app)
        {
            if (!app.Resources.ContainsKey("PrimaryAccentBrush")) app.Initialize();
            return;
        }
        new App().Initialize();
    }

    private sealed class TestSecretStore : ISecretStore
    {
        private readonly Dictionary<string, string> _values = new();
        public string? Get(string key) => _values.TryGetValue(key, out var value) ? value : null;
        public void Set(string key, string value) => _values[key] = value;
        public void Delete(string key) => _values.Remove(key);
    }

    private sealed class TestSettingsService : ISettingsService, IDisposable
    {
        private readonly Subject<SettingsModel> _changes = new();
        public SettingsModel Current { get; private set; } = SettingsModel.Defaults;
        public IObservable<SettingsModel> WhenChanged => new CountingObservable(_changes, this);
        public SettingsLoadResult LoadResult => SettingsLoadResult.Missing;
        public IObservable<SettingsSaveError> WriteErrors => Observable.Empty<SettingsSaveError>();
        public int SubscriptionDisposeCount { get; private set; }

        public void Publish(SettingsModel snapshot)
        {
            Current = snapshot;
            _changes.OnNext(snapshot);
        }

        public Task<SettingsMutationResult> ApplyAsync(SettingsModel expectedCurrent, SettingsModel next, CancellationToken ct = default) =>
            Task.FromResult<SettingsMutationResult>(new SettingsMutationResult.Applied(next, new SettingsSaveResult.Saved()));
        public Task<SettingsMutationResult> UpdateAsync(Func<SettingsModel, SettingsModel> producer, CancellationToken ct = default) =>
            ApplyAsync(Current, producer(Current), ct);
        public Task<SettingsSaveResult> SaveAsync(CancellationToken ct = default) =>
            Task.FromResult<SettingsSaveResult>(new SettingsSaveResult.Saved());
        public void Dispose() => _changes.Dispose();

        private sealed class CountingObservable(Subject<SettingsModel> source, TestSettingsService owner) : IObservable<SettingsModel>
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

    private sealed class EmptyLanguageSessionService : ILanguageSessionService
    {
        public LanguageSessionSnapshot Current { get; } = new(
            LanguageSessionState.Unavailable, 0, null, null, null, null);
        public IObservable<LanguageSessionSnapshot> WhenChanged { get; } =
            Observable.Empty<LanguageSessionSnapshot>();
        public ILanguageServerSession? TryGetReadySession(long generation) => null;
        public Task RestartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingScheduler : IScheduler
    {
        private readonly IScheduler _inner = ImmediateScheduler.Instance;
        public int ScheduleCount { get; private set; }
        public DateTimeOffset Now => _inner.Now;
        public IDisposable Schedule<TState>(TState state, Func<IScheduler, TState, IDisposable> action)
        {
            ScheduleCount++;
            return _inner.Schedule(state, action);
        }
        public IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action)
        {
            ScheduleCount++;
            return _inner.Schedule(state, dueTime, action);
        }
        public IDisposable Schedule<TState>(TState state, DateTimeOffset dueTime, Func<IScheduler, TState, IDisposable> action)
        {
            ScheduleCount++;
            return _inner.Schedule(state, dueTime, action);
        }
    }
}
