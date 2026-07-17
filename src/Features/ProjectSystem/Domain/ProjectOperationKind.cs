namespace Zaide.Features.ProjectSystem.Domain;

/// <summary>
/// Exclusive project operations coordinated by <see cref="IProjectOperationGate"/>.
/// </summary>
public enum ProjectOperationKind
{
    Build,
    Run,
    Test,
    DebugStart,
}