using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Services;
using Zaide.ViewModels;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Workspace.Infrastructure;
using Zaide.Features.Workspace.Presentation;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Infrastructure;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.ProjectSystem.Presentation;
using Zaide.Tests.Features.ProjectSystem;
using Zaide.Tests.Features.Debugging.Application;
using Zaide.Tests.Features.Debugging.Presentation;
using Zaide.Features.SourceControl.Contracts;
using Zaide.Features.SourceControl.Application;
using Zaide.Features.SourceControl.Presentation;
using Zaide.Features.Terminal.Contracts;
using Zaide.Features.Terminal.Application;
using Zaide.Features.Terminal.Infrastructure;
using Zaide.Features.Terminal.Presentation;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Presentation;

namespace Zaide.Tests.Features.ProjectSystem.Presentation;

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
    public void Workflow_CancelCommand_IsEnabledWhileTestOperationActive()
    {
        var registry = CommandRegistryFactory.Create();
        var workflowService = new RecordingWorkflowService();
        var workflowVm = TestProjectWorkflowFactory.Create(registry: registry, workflow: workflowService);
        workflowVm.Scheduler = CurrentThreadScheduler.Instance;
        workflowVm.Activate();

        var service = new FakeTestResultsService();
        using var vm = CreateViewModel(service, workflowVm);
        vm.Activate();

        workflowService.Emit(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Running,
            1,
            ProjectWorkflowOperation.Test,
            null,
            "/tmp/app.csproj",
            42,
            Array.Empty<ManagedProcessOutputLine>()));

        Assert.True(vm.Workflow.IsOperationActive);
        Assert.True(vm.Workflow.CancelCommand.CanExecute.FirstAsync().Wait());
        Assert.Equal("Cancel tests", vm.Workflow.CancelAutomationName);
    }

    [Theory]
    [InlineData(ProjectWorkflowOperation.Build, "Cancel build")]
    [InlineData(ProjectWorkflowOperation.Run, "Cancel run")]
    [InlineData(ProjectWorkflowOperation.Test, "Cancel tests")]
    public void Workflow_CancelAutomationName_MatchesActiveOperation(
        ProjectWorkflowOperation operation,
        string expectedName)
    {
        var workflowService = new RecordingWorkflowService();
        var workflowVm = TestProjectWorkflowFactory.Create(workflow: workflowService);
        workflowVm.Scheduler = CurrentThreadScheduler.Instance;
        workflowVm.Activate();

        workflowService.Emit(new ProjectWorkflowSnapshot(
            ProjectWorkflowOperationState.Running,
            1,
            operation,
            null,
            "/tmp/app.csproj",
            42,
            Array.Empty<ManagedProcessOutputLine>()));

        Assert.Equal(expectedName, workflowVm.CancelAutomationName);
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

    private static TestResultsViewModel CreateViewModel(
        FakeTestResultsService service,
        ProjectWorkflowViewModel? workflow = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new global::Zaide.Features.Workspace.Domain.Workspace());
        services.AddSingleton<IFileService, FileService>();
        services.AddTransient<EditorViewModel>();
        var sp = services.BuildServiceProvider();
        var workspace = sp.GetRequiredService<global::Zaide.Features.Workspace.Domain.Workspace>();
        var editorTabs = new EditorTabViewModel(sp, sp.GetRequiredService<IFileService>(), workspace);
        workflow ??= TestProjectWorkflowFactory.Create();
        var vm = new TestResultsViewModel(service, editorTabs, workflow);
        vm.Scheduler = CurrentThreadScheduler.Instance;
        return vm;
    }

    private static MainWindowViewModel CreateMainWindowViewModel(ICommandRegistry registry)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFileService, FileService>();
        services.AddTransient<EditorViewModel>();
        services.AddSingleton<global::Zaide.Features.Workspace.Domain.Workspace>();
        var sp = services.BuildServiceProvider();
        var workspace = sp.GetRequiredService<global::Zaide.Features.Workspace.Domain.Workspace>();
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
                new Moq.Mock<IGitMutationService>().Object,
                new Moq.Mock<IGitRepositoryService>().Object,
                commandRegistry: registry),
            TestProblemsFactory.Create(workspace, editorTabs),
            TestProjectWorkflowFactory.Create(registry: registry),
            TestTestResultsFactory.Create(editorTabs),
            TestDebugSessionFactory.Create(registry),
            TestDebugPanelFactory.Create(),
            TestEditorBreakpointFactory.Create(editorTabs, registry),
            workspace,
            projectContext.Object,
            registry);
    }

    private sealed class RecordingWorkflowService : IProjectWorkflowService
    {
        private readonly Subject<ProjectWorkflowSnapshot> _subject = new();
        private ProjectWorkflowSnapshot _current = new(
            ProjectWorkflowOperationState.Idle,
            0,
            null,
            null,
            null,
            null,
            Array.Empty<ManagedProcessOutputLine>());

        public ProjectWorkflowSnapshot Current => _current;

        public IObservable<ProjectWorkflowSnapshot> WhenChanged => _subject;

        public IObservable<ManagedProcessOutputLine> WhenOutputReceived =>
            new Subject<ManagedProcessOutputLine>();

        public void Emit(ProjectWorkflowSnapshot snapshot)
        {
            _current = snapshot;
            _subject.OnNext(snapshot);
        }

        public Task<ProjectWorkflowOperationResult> StartBuildAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProjectWorkflowOperationResult> StartRunAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProjectWorkflowOperationResult> StartTestAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProjectWorkflowOperationResult> StartBuildForDebugHandoffAsync(
            IProjectOperationHandoffLease handoffLease,
            CancellationToken cancellationToken = default) =>
            StartBuildAsync(cancellationToken);

        public Task CancelAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void Dispose()
        {
            _subject.OnCompleted();
            _subject.Dispose();
        }
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
