using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Zaide.Services;
using Zaide.ViewModels;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Tests.Features.ProjectSystem;

namespace Zaide.Tests;

/// <summary>
/// Shared factory for idle project workflow projections in composition tests.
/// </summary>
internal static class TestProjectWorkflowFactory
{
    public static ProjectWorkflowViewModel Create(
        ICommandRegistry? registry = null,
        IProjectContextService? projectContext = null,
        IProjectWorkflowService? workflow = null)
    {
        var context = projectContext ?? CreateIdleProjectContext();
        var wf = workflow ?? new IdleProjectWorkflowService();
        return CreateViewModel(wf, context, registry);
    }

    public static ProjectWorkflowViewModel CreateViewModel(
        IProjectWorkflowService workflow,
        IProjectContextService context,
        ICommandRegistry? registry = null)
    {
        var output = new ProjectOutputService(workflow);
        var gate = TestOperationGateFactory.CreateIdleGate();
        var debugSession = TestOperationGateFactory.CreateIdleDebugSession();
        return new ProjectWorkflowViewModel(
            workflow,
            output,
            context,
            gate,
            debugSession.Object,
            registry);
    }

    public static IProjectContextService CreateIdleProjectContext()
    {
        var mock = new Mock<IProjectContextService>();
        var unloaded = new ProjectContext(
            ProjectContextState.Unloaded,
            WorkspaceRoot: null,
            Candidates: Array.Empty<ProjectCandidate>(),
            SelectedProject: null,
            UnsupportedFiles: Array.Empty<string>(),
            ErrorMessage: null);
        mock.SetupGet(s => s.Current).Returns(unloaded);
        mock.SetupGet(s => s.WhenChanged).Returns(new Subject<ProjectContext>());
        return mock.Object;
    }

    internal sealed class IdleProjectWorkflowService : IProjectWorkflowService
    {
        private static readonly ProjectWorkflowSnapshot Idle = new(
            ProjectWorkflowOperationState.Idle,
            Generation: 0,
            ActiveOperation: null,
            LastOutcome: null,
            TargetFilePath: null,
            ProcessId: null,
            OutputLines: Array.Empty<ManagedProcessOutputLine>());

        private readonly Subject<ProjectWorkflowSnapshot> _subject = new();

        public ProjectWorkflowSnapshot Current => Idle;

        public IObservable<ProjectWorkflowSnapshot> WhenChanged => _subject;

        public IObservable<ManagedProcessOutputLine> WhenOutputReceived =>
            new Subject<ManagedProcessOutputLine>();

        public Task<ProjectWorkflowOperationResult> StartBuildAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProjectWorkflowOperationResult(
                ProjectWorkflowOutcomeKind.RejectedContext,
                0,
                ProjectWorkflowOperation.Build,
                null,
                null));

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
}
