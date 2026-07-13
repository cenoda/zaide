using System;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.ViewModels;

namespace Zaide.Tests.ViewModels;

/// <summary>
/// Phase 11 M5 tests for Test Results panel projection.
/// </summary>
public sealed class TestResultsViewModelTests
{
    static TestResultsViewModelTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    [Fact]
    public void ApplySnapshot_ProjectsCasesAndSummary()
    {
        var service = new FakeTestResultsService();
        using var vm = CreateViewModel(service);
        vm.Activate();

        service.Publish(new TestResultsSnapshot(
            1,
            ProjectWorkflowOutcomeKind.Failed,
            IsPartial: false,
            new TestResultsSummary(0, 1, 0, 1),
            new[]
            {
                new TestCaseResult(
                    "Ns.Test.Fail",
                    "Ns.Test.Fail",
                    TestCaseOutcome.Failed,
                    "3 ms",
                    "boom",
                    null,
                    null,
                    null),
            }));

        Assert.Single(vm.Cases);
        Assert.Contains("Failed: 1", vm.SummaryText);
        Assert.Contains("failed", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplySnapshot_PartialWithoutCases_SurfacesOutputFallbackStatus()
    {
        var service = new FakeTestResultsService();
        using var vm = CreateViewModel(service);
        vm.Activate();

        service.Publish(new TestResultsSnapshot(
            2,
            ProjectWorkflowOutcomeKind.Failed,
            IsPartial: true,
            Summary: null,
            Array.Empty<TestCaseResult>()));

        Assert.Empty(vm.Cases);
        Assert.Contains("Output", vm.StatusMessage);
    }

    [Fact]
    public void SwitchToTestResultsBottomCommand_SetsMode()
    {
        var registry = CommandRegistryFactory.Create();
        var main = CreateMainWindowViewModel(registry);
        main.Activate();

        main.SwitchToTestResultsBottomCommand.Execute().Subscribe();

        Assert.Equal(BottomPanelMode.TestResults, main.BottomPanelMode);
        Assert.True(main.IsBottomPanelVisible);
        Assert.True(main.IsTestResultsBottomMode);
    }

    private static TestResultsViewModel CreateViewModel(FakeTestResultsService service)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new Workspace());
        services.AddSingleton<IFileService, FileService>();
        services.AddTransient<EditorViewModel>();
        var sp = services.BuildServiceProvider();
        var workspace = sp.GetRequiredService<Workspace>();
        var editorTabs = new EditorTabViewModel(sp, sp.GetRequiredService<IFileService>(), workspace);
        var vm = new TestResultsViewModel(service, editorTabs);
        vm.Scheduler = CurrentThreadScheduler.Instance;
        return vm;
    }

    private static MainWindowViewModel CreateMainWindowViewModel(ICommandRegistry registry)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFileService, FileService>();
        services.AddTransient<EditorViewModel>();
        services.AddSingleton<Workspace>();
        var sp = services.BuildServiceProvider();
        var workspace = sp.GetRequiredService<Workspace>();
        var fileTree = new FileTreeViewModel(new FileTreeService(), CurrentThreadScheduler.Instance);
        var editorTabs = new EditorTabViewModel(sp, sp.GetRequiredService<IFileService>(), workspace);
        var terminalService = new Moq.Mock<ITerminalService>();
        var terminalViewModel = new TerminalViewModel(terminalService.Object, a => a());
        var factory = new Moq.Mock<ITerminalSessionFactory>();
        factory.Setup(f => f.CreateSession()).Returns(terminalViewModel);
        var terminalHost = new TerminalHost(factory.Object);
        var coordinator = new Moq.Mock<IAgentExecutionCoordinator>().Object;
        var panelHost = new AgentPanelHost();
        var parser = new MentionParser(panelHost);
        var router = new AgentRouter(parser, panelHost, coordinator);

        var projectContext = new Moq.Mock<IProjectContextService>(Moq.MockBehavior.Loose);
        projectContext.Setup(s => s.WhenChanged).Returns(System.Reactive.Linq.Observable.Never<ProjectContext>());

        return new MainWindowViewModel(
            fileTree,
            editorTabs,
            terminalHost,
            panelHost,
            coordinator,
            router,
            new TownhallViewModel(new TownhallState()),
            new SourceControlViewModel(
                new SourceControlSnapshotOrchestrator(new Moq.Mock<IGitRepositoryService>().Object),
                workspace,
                new Moq.Mock<IFileDiffService>().Object,
                new Moq.Mock<IGitMutationService>().Object,
                new Moq.Mock<IGitRepositoryService>().Object,
                registry),
            TestProblemsFactory.Create(workspace, editorTabs),
            TestProjectWorkflowFactory.Create(registry: registry),
            TestTestResultsFactory.Create(editorTabs),
            workspace,
            projectContext.Object,
            registry);
    }

    private sealed class FakeTestResultsService : ITestResultsService
    {
        private readonly Subject<TestResultsSnapshot> _subject = new();
        private TestResultsSnapshot _current = TestResultsSnapshot.Empty;

        public TestResultsSnapshot Current => _current;

        public IObservable<TestResultsSnapshot> WhenChanged => _subject;

        public void Publish(TestResultsSnapshot snapshot)
        {
            _current = snapshot;
            _subject.OnNext(snapshot);
        }

        public void Dispose()
        {
            _subject.OnCompleted();
            _subject.Dispose();
        }
    }
}
