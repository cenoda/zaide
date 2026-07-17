namespace Zaide.Features.ProjectSystem.Domain;

/// <summary>
/// Structured result returned by workflow start methods.
/// </summary>
/// <param name="Outcome">Terminal or admission-control outcome.</param>
/// <param name="Generation">Operation generation for accepted starts.</param>
/// <param name="Operation">The requested operation.</param>
/// <param name="TargetFilePath">
/// Resolved target path for accepted starts, otherwise <c>null</c>.
/// </param>
/// <param name="ExitCode">
/// Process exit code for completed runs, otherwise <c>null</c>.
/// </param>
public sealed record ProjectWorkflowOperationResult(
    ProjectWorkflowOutcomeKind Outcome,
    long Generation,
    ProjectWorkflowOperation Operation,
    string? TargetFilePath,
    int? ExitCode);
