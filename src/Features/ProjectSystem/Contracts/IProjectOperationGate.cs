using System;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.ProjectSystem.Domain;

namespace Zaide.Features.ProjectSystem.Contracts;

/// <summary>
/// UI-independent admission gate shared by workflow and debug-start operations.
/// Build, Run, Test, and Debug Start are mutually exclusive.
/// </summary>
public interface IProjectOperationGate : IDisposable
{
    /// <summary>The active workflow operation, if any.</summary>
    ProjectOperationKind? ActiveWorkflowOperation { get; }

    /// <summary>Whether a debug handoff lease is currently active.</summary>
    bool IsDebugHandoffActive { get; }

    /// <summary>
    /// Attempts to admit one Build, Run, or Test operation.
    /// </summary>
    Task<ProjectOperationAcquireResult> TryAcquireWorkflowOperationAsync(
        ProjectOperationKind kind,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to admit a debug-start handoff lease held until explicit release.
    /// </summary>
    Task<ProjectOperationAcquireResult> TryAcquireDebugHandoffAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Releases an active debug handoff lease.</summary>
    void ReleaseDebugHandoff(IProjectOperationHandoffLease lease);

    /// <summary>
    /// Enters the gate critical section used for workflow snapshot mutations.
    /// </summary>
    Task EnterCriticalSectionAsync(CancellationToken cancellationToken = default);

    /// <summary>Exits the gate critical section.</summary>
    void ExitCriticalSection();
}