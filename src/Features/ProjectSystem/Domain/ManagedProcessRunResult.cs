namespace Zaide.Features.ProjectSystem.Domain;

/// <summary>
/// Result of one <see cref="IManagedProcessRunner.RunAsync"/> invocation.
/// </summary>
/// <param name="ExitCode">
/// Process exit code when the process started and exited, otherwise <c>null</c>.
/// </param>
/// <param name="WasCancelled">
/// <c>true</c> when the run was cancelled before a successful terminal exit.
/// </param>
/// <param name="StartupFailed">
/// <c>true</c> when the executable could not be started.
/// </param>
public sealed record ManagedProcessRunResult(
    int? ExitCode,
    bool WasCancelled,
    bool StartupFailed);
