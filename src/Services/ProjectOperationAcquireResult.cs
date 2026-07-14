namespace Zaide.Services;

/// <summary>
/// Result of an admission attempt against <see cref="IProjectOperationGate"/>.
/// </summary>
/// <param name="IsSuccess">Whether admission was granted.</param>
/// <param name="Lease">Active lease when <paramref name="IsSuccess"/> is true.</param>
/// <param name="RejectionReason">Structured rejection reason when admission failed.</param>
/// <param name="Message">Operator-facing rejection message when admission failed.</param>
public sealed record ProjectOperationAcquireResult(
    bool IsSuccess,
    IProjectOperationLease? Lease,
    ProjectOperationRejectionReason? RejectionReason,
    string? Message);