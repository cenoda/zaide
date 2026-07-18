using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;

namespace Zaide.Features.ProjectSystem.Infrastructure;

/// <summary>
/// Singleton workflow owner. Resolves targets from <see cref="IProjectContextService"/>,
/// runs one redirected dotnet process at a time, and cancels on context invalidation.
/// </summary>
internal sealed class ProjectWorkflowService : IProjectWorkflowService
{
    private static readonly ProjectWorkflowSnapshot InitialSnapshot = new(
        ProjectWorkflowOperationState.Idle,
        Generation: 0,
        ActiveOperation: null,
        LastOutcome: null,
        TargetFilePath: null,
        ProcessId: null,
        OutputLines: Array.Empty<ManagedProcessOutputLine>(),
        LastOperation: null);

    private readonly IProjectContextService _projectContext;
    private readonly IProjectOperationGate _operationGate;
    private readonly IManagedProcessRunner _runner;
    private readonly ILogger<ProjectWorkflowService> _logger;
    private readonly Subject<ProjectWorkflowSnapshot> _snapshotSubject = new();
    private readonly Subject<ManagedProcessOutputLine> _outputSubject = new();
    private readonly IDisposable _contextSubscription;
    private readonly List<ManagedProcessOutputLine> _outputLines = new();

    private volatile ProjectWorkflowSnapshot _current = InitialSnapshot;
    private long _operationGeneration;
    private CancellationTokenSource? _operationCts;
    private bool _disposed;

    public ProjectWorkflowService(
        IProjectContextService projectContext,
        IProjectOperationGate operationGate,
        IManagedProcessRunner runner,
        ILogger<ProjectWorkflowService> logger)
    {
        _projectContext = projectContext ?? throw new ArgumentNullException(nameof(projectContext));
        _operationGate = operationGate ?? throw new ArgumentNullException(nameof(operationGate));
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _contextSubscription = _projectContext.WhenChanged.Subscribe(OnProjectContextChanged);
    }

    /// <inheritdoc />
    public ProjectWorkflowSnapshot Current => _current;

    /// <inheritdoc />
    public IObservable<ProjectWorkflowSnapshot> WhenChanged => _snapshotSubject;

    /// <inheritdoc />
    public IObservable<ManagedProcessOutputLine> WhenOutputReceived => _outputSubject;

    /// <inheritdoc />
    public Task<ProjectWorkflowOperationResult> StartBuildAsync(
        CancellationToken cancellationToken = default) =>
        StartOperationAsync(ProjectWorkflowOperation.Build, cancellationToken);

    /// <inheritdoc />
    public Task<ProjectWorkflowOperationResult> StartBuildForDebugHandoffAsync(
        IProjectOperationHandoffLease handoffLease,
        CancellationToken cancellationToken = default) =>
        StartOperationAsync(
            ProjectWorkflowOperation.Build,
            cancellationToken,
            handoffLease);

    /// <inheritdoc />
    public Task<ProjectWorkflowOperationResult> StartRunAsync(
        CancellationToken cancellationToken = default) =>
        StartOperationAsync(ProjectWorkflowOperation.Run, cancellationToken);

    /// <inheritdoc />
    public Task<ProjectWorkflowOperationResult> StartTestAsync(
        CancellationToken cancellationToken = default) =>
        StartOperationAsync(ProjectWorkflowOperation.Test, cancellationToken);

    /// <inheritdoc />
    public async Task CancelAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        CancellationTokenSource? cts;
        await _operationGate.EnterCriticalSectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cts = _operationCts;
        }
        finally
        {
            _operationGate.ExitCriticalSection();
        }

        if (cts is not null)
        {
            await cts.CancelAsync().ConfigureAwait(false);
            await _runner.KillAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        // Publish the disposed flag before taking the critical section or
        // killing the runner so in-flight output/completion paths exit early and
        // never block dispose behind a background AppendOutputLine waiter.
        _disposed = true;

        _contextSubscription.Dispose();

        _operationCts?.Cancel();
        _operationCts?.Dispose();
        _operationCts = null;

        try
        {
            _runner.KillAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.Log(
                LogLevel.Debug,
                new EventId(11001),
                ex,
                "Project workflow dispose kill encountered an error.");
        }

        _runner.Dispose();

        try
        {
            _operationGate.EnterCriticalSectionAsync().GetAwaiter().GetResult();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            PublishLocked(InitialSnapshot);
            _snapshotSubject.OnCompleted();
            _snapshotSubject.Dispose();
            _outputSubject.OnCompleted();
            _outputSubject.Dispose();
        }
        finally
        {
            _operationGate.ExitCriticalSection();
        }
    }

    private void OnProjectContextChanged(ProjectContext context)
    {
        if (_disposed)
            return;

        ObserveTask(HandleContextChangeAsync(context));
    }

    private async Task HandleContextChangeAsync(ProjectContext context)
    {
        var shouldKill = false;

        await _operationGate.EnterCriticalSectionAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
                return;

            if (_current.State is not ProjectWorkflowOperationState.Starting
                and not ProjectWorkflowOperationState.Running)
            {
                return;
            }

            var activePath = _current.TargetFilePath;
            if (ProjectTargetResolver.IsEligible(context) &&
                context.SelectedProject is not null &&
                string.Equals(
                    Path.GetFullPath(context.SelectedProject.FilePath),
                    activePath,
                    StringComparison.Ordinal))
            {
                return;
            }

            var generation = _current.Generation;
            var targetFilePath = _current.TargetFilePath;
            var lastOperation = _current.ActiveOperation ?? _current.LastOperation;

            _operationGeneration++;
            _operationCts?.Cancel();

            PublishLocked(new ProjectWorkflowSnapshot(
                ProjectWorkflowOperationState.Idle,
                generation,
                ActiveOperation: null,
                ProjectWorkflowOutcomeKind.Cancelled,
                targetFilePath,
                ProcessId: null,
                _outputLines.ToArray(),
                LastOperation: lastOperation));

            shouldKill = true;
        }
        finally
        {
            _operationGate.ExitCriticalSection();
        }

        if (shouldKill)
            await _runner.KillAsync().ConfigureAwait(false);
    }

    private async Task<ProjectWorkflowOperationResult> StartOperationAsync(
        ProjectWorkflowOperation operation,
        CancellationToken cancellationToken,
        IProjectOperationHandoffLease? debugHandoffLease = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        IProjectOperationLease? admissionLease = null;
        if (debugHandoffLease is null)
        {
            var acquire = await _operationGate.TryAcquireWorkflowOperationAsync(
                MapOperationKind(operation),
                cancellationToken).ConfigureAwait(false);
            if (!acquire.IsSuccess)
            {
                return new ProjectWorkflowOperationResult(
                    ProjectWorkflowOutcomeKind.RejectedConcurrent,
                    _current.Generation,
                    operation,
                    _current.TargetFilePath,
                    ExitCode: null);
            }

            admissionLease = acquire.Lease;
        }
        else
        {
            if (operation != ProjectWorkflowOperation.Build)
            {
                throw new InvalidOperationException(
                    "Debug handoff builds are limited to the Build operation.");
            }

            ValidateDebugHandoffLease(debugHandoffLease);
        }

        try
        {
            ResolvedProjectTarget target;
            ProjectExecutionProfile profile;
            long generation;
            CancellationTokenSource operationCts;

            await _operationGate.EnterCriticalSectionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                if (debugHandoffLease is null &&
                    _current.State is ProjectWorkflowOperationState.Starting
                        or ProjectWorkflowOperationState.Running)
                {
                    return new ProjectWorkflowOperationResult(
                        ProjectWorkflowOutcomeKind.RejectedConcurrent,
                        _current.Generation,
                        operation,
                        _current.TargetFilePath,
                        ExitCode: null);
                }

                var resolution = ProjectTargetResolver.Resolve(_projectContext.Current, operation);
                if (!resolution.IsSuccess)
                {
                    return new ProjectWorkflowOperationResult(
                        ProjectWorkflowOutcomeKind.RejectedContext,
                        _current.Generation,
                        operation,
                        TargetFilePath: null,
                        ExitCode: null);
                }

                target = resolution.Target!;
                profile = ProjectExecutionProfileResolver.Resolve(target, operation);

                _operationGeneration++;
                generation = _operationGeneration;

                _operationCts?.Cancel();
                _operationCts?.Dispose();
                operationCts = new CancellationTokenSource();
                _operationCts = operationCts;

                _outputLines.Clear();
                PublishLocked(new ProjectWorkflowSnapshot(
                    ProjectWorkflowOperationState.Starting,
                    generation,
                    operation,
                    LastOutcome: null,
                    target.FilePath,
                    ProcessId: null,
                    OutputLines: Array.Empty<ManagedProcessOutputLine>(),
                    LastOperation: operation));
            }
            finally
            {
                _operationGate.ExitCriticalSection();
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                operationCts.Token);

            void OnOutput(ManagedProcessOutputLine line)
            {
                if (line.Generation != generation)
                    return;

                AppendOutputLine(line);
            }

            void OnProcessStarted()
            {
                PublishRunningIfCurrent(
                    generation,
                    operation,
                    target.FilePath,
                    _runner.ProcessId);
            }

            _runner.OutputReceived += OnOutput;
            _runner.ProcessStarted += OnProcessStarted;
            try
            {
                var request = new ManagedProcessStartRequest(
                    profile.FileName,
                    profile.Arguments,
                    profile.WorkingDirectory,
                    generation);

                var runResult = await _runner.RunAsync(request, linked.Token).ConfigureAwait(false);
                var outcome = MapRunResult(runResult);

                return await CompleteOperationAsync(
                    generation,
                    operation,
                    target.FilePath,
                    outcome,
                    runResult.ExitCode).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (operationCts.IsCancellationRequested)
            {
                return await CompleteOperationAsync(
                    generation,
                    operation,
                    target.FilePath,
                    ProjectWorkflowOutcomeKind.Cancelled,
                    exitCode: null).ConfigureAwait(false);
            }
            finally
            {
                _runner.ProcessStarted -= OnProcessStarted;
                _runner.OutputReceived -= OnOutput;
            }
        }
        finally
        {
            admissionLease?.Dispose();
        }
    }

    private async Task<ProjectWorkflowOperationResult> CompleteOperationAsync(
        long generation,
        ProjectWorkflowOperation operation,
        string targetFilePath,
        ProjectWorkflowOutcomeKind outcome,
        int? exitCode)
    {
        if (_disposed || generation != _operationGeneration)
        {
            return new ProjectWorkflowOperationResult(
                outcome,
                generation,
                operation,
                targetFilePath,
                exitCode);
        }

        try
        {
            await _operationGate.EnterCriticalSectionAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            return new ProjectWorkflowOperationResult(
                outcome,
                generation,
                operation,
                targetFilePath,
                exitCode);
        }
        try
        {
            if (_disposed || generation != _operationGeneration)
            {
                return new ProjectWorkflowOperationResult(
                    outcome,
                    generation,
                    operation,
                    targetFilePath,
                    exitCode);
            }

            if (generation == _operationGeneration)
            {
                _operationCts?.Dispose();
                _operationCts = null;
            }

            var outputCopy = _outputLines.ToArray();
            PublishLocked(new ProjectWorkflowSnapshot(
                ProjectWorkflowOperationState.Idle,
                generation,
                ActiveOperation: null,
                outcome,
                targetFilePath,
                ProcessId: null,
                outputCopy,
                LastOperation: operation));

            return new ProjectWorkflowOperationResult(
                outcome,
                generation,
                operation,
                targetFilePath,
                exitCode);
        }
        finally
        {
            _operationGate.ExitCriticalSection();
        }
    }

    private void PublishRunningIfCurrent(
        long generation,
        ProjectWorkflowOperation operation,
        string targetFilePath,
        int? processId)
    {
        if (_disposed || generation != _operationGeneration)
            return;

        try
        {
            _operationGate.EnterCriticalSectionAsync().GetAwaiter().GetResult();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            if (_disposed || generation != _operationGeneration)
                return;

            PublishLocked(new ProjectWorkflowSnapshot(
                ProjectWorkflowOperationState.Running,
                generation,
                operation,
                LastOutcome: null,
                targetFilePath,
                processId,
                _outputLines.ToArray(),
                LastOperation: operation));
        }
        finally
        {
            _operationGate.ExitCriticalSection();
        }
    }

    private void AppendOutputLine(ManagedProcessOutputLine line)
    {
        // Early exit: if already disposed or the line is from a stale
        // generation we can skip without touching the gate at all.
        if (_disposed || line.Generation != _operationGeneration)
            return;

        try
        {
            _operationGate.EnterCriticalSectionAsync().GetAwaiter().GetResult();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            if (_disposed || line.Generation != _operationGeneration)
                return;

            _outputLines.Add(line);
            _outputSubject.OnNext(line);

            if (_current.Generation == line.Generation)
            {
                _current = _current with
                {
                    OutputLines = _outputLines.ToArray(),
                };
            }
        }
        finally
        {
            _operationGate.ExitCriticalSection();
        }
    }

    private static ProjectWorkflowOutcomeKind MapRunResult(ManagedProcessRunResult runResult)
    {
        if (runResult.StartupFailed)
            return ProjectWorkflowOutcomeKind.StartupFailed;

        if (runResult.WasCancelled)
            return ProjectWorkflowOutcomeKind.Cancelled;

        if (runResult.ExitCode == 0)
            return ProjectWorkflowOutcomeKind.Succeeded;

        return ProjectWorkflowOutcomeKind.Failed;
    }

    private void PublishLocked(ProjectWorkflowSnapshot snapshot)
    {
        _current = snapshot;
        _snapshotSubject.OnNext(snapshot);
    }

    private void ValidateDebugHandoffLease(IProjectOperationHandoffLease lease)
    {
        if (_operationGate is not ProjectOperationGate concrete)
        {
            if (!lease.IsActive)
                throw new InvalidOperationException("An active debug handoff lease is required.");

            return;
        }

        concrete.ValidateDebugHandoff(lease);
    }

    private static ProjectOperationKind MapOperationKind(ProjectWorkflowOperation operation) =>
        operation switch
        {
            ProjectWorkflowOperation.Build => ProjectOperationKind.Build,
            ProjectWorkflowOperation.Run => ProjectOperationKind.Run,
            ProjectWorkflowOperation.Test => ProjectOperationKind.Test,
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
        };

    private static void ObserveTask(Task task)
    {
        _ = task.ContinueWith(
            t => { _ = t.Exception; },
            TaskScheduler.Default);
    }
}
