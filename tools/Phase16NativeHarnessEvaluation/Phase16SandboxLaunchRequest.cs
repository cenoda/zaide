namespace Phase16NativeHarnessEvaluation;

public sealed class Phase16SandboxLaunchRequest
{
    public required string ExecutablePath { get; init; }
    public required IReadOnlyList<string> Arguments { get; init; }
    public required IReadOnlyDictionary<string, string> AllowedEnvironment { get; init; }
    public required string WorkingDirectory { get; init; }
    public required IReadOnlyList<string> WritableRootPaths { get; init; }
    public TimeSpan? WallTimeout { get; init; }
    public CancellationToken CancellationToken { get; init; }
    public string? TrialMarker { get; init; }
}

public sealed class Phase16SandboxLaunchResult
{
    public required int ExitCode { get; init; }
    public required bool TimedOut { get; init; }
    public required bool Cancelled { get; init; }
    public required string Stdout { get; init; }
    public required string Stderr { get; init; }
    public required bool StdoutTruncated { get; init; }
    public required bool StderrTruncated { get; init; }
    public required IReadOnlyList<string> ExactArgv { get; init; }
    public required IReadOnlyDictionary<string, string> AppliedEnvironment { get; init; }
    public required bool OrphanProcessesDetected { get; init; }
    public required IReadOnlyList<string> LifecycleEvents { get; init; }
}
