namespace Zaide.Features.ProjectSystem.Domain;

/// <summary>
/// Identifies which standard stream produced a managed-process output line.
/// </summary>
public enum ProcessStreamKind
{
    StdOut,
    StdErr,
}
