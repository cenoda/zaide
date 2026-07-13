namespace Zaide.Services;

/// <summary>
/// A build/run/test workflow operation started through
/// <see cref="IProjectWorkflowService"/>.
/// </summary>
public enum ProjectWorkflowOperation
{
    Build,
    Run,
    Test,
}
