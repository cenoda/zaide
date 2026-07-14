namespace Zaide.Services;

/// <summary>
/// Builds locked default execution profiles from a resolved project target.
/// </summary>
public static class ProjectExecutionProfileResolver
{
    /// <summary>
    /// Resolves the default profile for <paramref name="operation"/>.
    /// </summary>
    public static ProjectExecutionProfile Resolve(
        ResolvedProjectTarget target,
        ProjectWorkflowOperation operation)
    {
        var quotedPath = QuotePath(target.FilePath);

        return operation switch
        {
            ProjectWorkflowOperation.Build => new ProjectExecutionProfile(
                "dotnet",
                $"build {quotedPath}",
                target.WorkingDirectory),

            // Locked argv omits --logger / verbosity flags (F7): default dotnet test
            // emits a summary banner sufficient for counts; per-case detail appears on
            // failures without extra flags. Parser handles VSTest/xUnit variants fail-open.
            ProjectWorkflowOperation.Test => new ProjectExecutionProfile(
                "dotnet",
                $"test {quotedPath}",
                target.WorkingDirectory),

            ProjectWorkflowOperation.Run => new ProjectExecutionProfile(
                "dotnet",
                $"run --project {quotedPath}",
                target.WorkingDirectory),

            _ => throw new System.ArgumentOutOfRangeException(nameof(operation), operation, null),
        };
    }

    private static string QuotePath(string path) => $"\"{path}\"";
}
