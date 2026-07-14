using System;
using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Services;

/// <summary>
/// Shared singleton admission gate for workflow and debug-start operations.
/// Admission and workflow critical sections use separate mutexes so dispose/output
/// paths cannot deadlock across process kill.
/// </summary>
public sealed class ProjectOperationGate : IProjectOperationGate
{
    private readonly IDebugSessionService _debugSession;
    private readonly SemaphoreSlim _admissionMutex = new(1, 1);
    private readonly SemaphoreSlim _criticalSectionMutex = new(1, 1);
    private ProjectOperationKind? _activeWorkflowOperation;
    private HandoffLease? _activeHandoffLease;
    private bool _disposed;

    public ProjectOperationGate(IDebugSessionService debugSession)
    {
        _debugSession = debugSession ?? throw new ArgumentNullException(nameof(debugSession));
    }

    /// <inheritdoc />
    public ProjectOperationKind? ActiveWorkflowOperation => _activeWorkflowOperation;

    /// <inheritdoc />
    public bool IsDebugHandoffActive => _activeHandoffLease?.IsActive == true;

    /// <inheritdoc />
    public async Task<ProjectOperationAcquireResult> TryAcquireWorkflowOperationAsync(
        ProjectOperationKind kind,
        CancellationToken cancellationToken = default)
    {
        if (kind is not (ProjectOperationKind.Build or ProjectOperationKind.Run or ProjectOperationKind.Test))
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Workflow admission is limited to Build, Run, or Test.");

        await _admissionMutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (IsDebugHandoffActive || IsDebugSessionBlocking(_debugSession.Current.State))
            {
                return Reject(ProjectOperationRejectionReason.DebugSessionActive);
            }

            if (_activeWorkflowOperation is not null)
            {
                return Reject(ProjectOperationRejectionReason.WorkflowBusy);
            }

            _activeWorkflowOperation = kind;
            return new ProjectOperationAcquireResult(
                true,
                new WorkflowOperationLease(this, kind),
                null,
                null);
        }
        finally
        {
            _admissionMutex.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ProjectOperationAcquireResult> TryAcquireDebugHandoffAsync(
        CancellationToken cancellationToken = default)
    {
        await _admissionMutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (IsDebugHandoffActive)
            {
                return Reject(ProjectOperationRejectionReason.DebugSessionActive);
            }

            if (_activeWorkflowOperation is not null)
            {
                return Reject(ProjectOperationRejectionReason.WorkflowBusy);
            }

            if (IsDebugSessionBlocking(_debugSession.Current.State))
            {
                return Reject(ProjectOperationRejectionReason.DebugSessionActive);
            }

            var lease = new HandoffLease(this);
            _activeHandoffLease = lease;
            return new ProjectOperationAcquireResult(true, lease, null, null);
        }
        finally
        {
            _admissionMutex.Release();
        }
    }

    /// <inheritdoc />
    public void ReleaseDebugHandoff(IProjectOperationHandoffLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);

        _admissionMutex.Wait();
        try
        {
            if (_disposed)
                return;

            if (ReferenceEquals(_activeHandoffLease, lease))
            {
                _activeHandoffLease.MarkReleased();
                _activeHandoffLease = null;
            }
        }
        finally
        {
            _admissionMutex.Release();
        }
    }

    /// <inheritdoc />
    public async Task EnterCriticalSectionAsync(CancellationToken cancellationToken = default)
    {
        await _criticalSectionMutex.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void ExitCriticalSection()
    {
        try
        {
            _criticalSectionMutex.Release();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SemaphoreFullException)
        {
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _admissionMutex.Wait();
        try
        {
            if (_disposed)
                return;

            _disposed = true;
            _activeWorkflowOperation = null;
            _activeHandoffLease?.MarkReleased();
            _activeHandoffLease = null;
        }
        finally
        {
            _admissionMutex.Release();
        }

        _admissionMutex.Dispose();
        _criticalSectionMutex.Dispose();
    }

    internal void ReleaseWorkflowOperation(ProjectOperationKind kind)
    {
        _admissionMutex.Wait();
        try
        {
            if (_disposed)
                return;

            if (_activeWorkflowOperation == kind)
                _activeWorkflowOperation = null;
        }
        finally
        {
            _admissionMutex.Release();
        }
    }

    internal void ValidateDebugHandoff(IProjectOperationHandoffLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);

        _admissionMutex.Wait();
        try
        {
            if (!ReferenceEquals(_activeHandoffLease, lease) || !lease.IsActive)
                throw new InvalidOperationException("An active debug handoff lease is required.");
        }
        finally
        {
            _admissionMutex.Release();
        }
    }

    private static bool IsDebugSessionBlocking(DebugSessionState state) =>
        state is DebugSessionState.Starting
            or DebugSessionState.Running
            or DebugSessionState.Stopped
            or DebugSessionState.Stopping;

    private static ProjectOperationAcquireResult Reject(ProjectOperationRejectionReason reason) =>
        new(
            false,
            null,
            reason,
            reason switch
            {
                ProjectOperationRejectionReason.WorkflowBusy => ProjectOperationGateMessages.WorkflowBusy,
                ProjectOperationRejectionReason.DebugSessionActive => ProjectOperationGateMessages.DebugSessionActive,
                _ => null,
            });

    private sealed class WorkflowOperationLease : IProjectOperationLease
    {
        private readonly ProjectOperationGate _owner;
        private bool _released;

        public WorkflowOperationLease(ProjectOperationGate owner, ProjectOperationKind kind)
        {
            _owner = owner;
            Kind = kind;
        }

        public ProjectOperationKind Kind { get; }

        public void Dispose()
        {
            if (_released)
                return;

            _released = true;
            _owner.ReleaseWorkflowOperation(Kind);
        }
    }

    private sealed class HandoffLease : IProjectOperationHandoffLease
    {
        private readonly ProjectOperationGate _owner;
        private bool _released;

        public HandoffLease(ProjectOperationGate owner) => _owner = owner;

        public ProjectOperationKind Kind => ProjectOperationKind.DebugStart;

        public bool IsActive => !_released;

        public void Dispose()
        {
            if (_released)
                return;

            _owner.ReleaseDebugHandoff(this);
        }

        internal void MarkReleased() => _released = true;
    }
}