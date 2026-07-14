using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Zaide.Services;

/// <summary>
/// Singleton workflow owner. Resolves targets from <see cref="IProjectContextService"/>,
/// runs one redirected dotnet process at a time, and cancels on context invalidation.
/// </summary>
public sealed class ProjectWorkflowService : IProjectWorkflowService
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
    private readonly IManagedProcessRunner _runner;
    private readonly ILogger<ProjectWorkflowService> _logger;
    private readonly Subject<ProjectWorkflowSnapshot> _snapshotSubject = new();
    private readonly Subject<ManagedProcessOutputLine> _outputSubject = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IDisposable _contextSubscription;
    private readonly List<ManagedProcessOutputLine> _outputLines = new();

    private volatile ProjectWorkflowSnapshot _current = InitialSnapshot;
    private long _operationGeneration;
    private CancellationTokenSource? _operationCts;
    private bool _disposed;

    public ProjectWorkflowService(
        IProjectContextService projectContext,
        IManagedProcessRunner runner,
        ILogger<ProjectWorkflowService> logger)
    {
        _projectContext = projectContext ?? throw new ArgumentNullException(nameof(projectContext));
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
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cts = _operationCts;
        }
        finally
        {
            _gate.Release();
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

        PublishLocked(InitialSnapshot);
        _snapshotSubject.OnCompleted();
        _snapshotSubject.Dispose();
        _outputSubject.OnCompleted();
        _outputSubject.Dispose();
        _gate.Dispose();
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

        await _gate.WaitAsync().ConfigureAwait(false);
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
                context.SelectedProject?.FilePath == activePath)
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
            _gate.Release();
        }

        if (shouldKill)
            await _runner.KillAsync().ConfigureAwait(false);
    }

    private async Task<ProjectWorkflowOperationResult> StartOperationAsync(
        ProjectWorkflowOperation operation,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        ResolvedProjectTarget target;
        ProjectExecutionProfile profile;
        long generation;
        CancellationTokenSource operationCts;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_current.State is ProjectWorkflowOperationState.Starting
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
            _gate.Release();
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

        _runner.OutputReceived += OnOutput;
        try
        {
            PublishRunningIfCurrent(generation, operation, target.FilePath, _runner.ProcessId);

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
            _runner.OutputReceived -= OnOutput;
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

        await _gate.WaitAsync().ConfigureAwait(false);
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
            _gate.Release();
        }
    }

    private void PublishRunningIfCurrent(
        long generation,
        ProjectWorkflowOperation operation,
        string targetFilePath,
        int? processId)
    {
        _gate.Wait();
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
            _gate.Release();
        }
    }

    private void AppendOutputLine(ManagedProcessOutputLine line)
    {
        _gate.Wait();
        try
        {
            if (_disposed || line.Generation != _operationGeneration)
                return;

            _outputLines.Add(line);
            _outputSubject.OnNext(line);

            // Update _current silently so the Current property reflects
            // accumulated lines for pollers (e.g. activation seed). Do NOT
            // publish through _snapshotSubject — per-line snapshots are the
            // O(n²) projection this fix eliminates. State-transition publishes
            // (PublishRunningIfCurrent, CompleteOperationAsync, context-change)
            // still carry full OutputLines for diagnostic/test consumers.
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
            _gate.Release();
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

    private static void ObserveTask(Task task)
    {
        _ = task.ContinueWith(
            t => { _ = t.Exception; },
            TaskScheduler.Default);
    }
}
