namespace Phase16NativeHarnessEvaluation;

public sealed class FakeCandidateRunResult
{
    public required TrialMetrics Metrics { get; init; }
    public required string Stdout { get; init; }
    public required string Stderr { get; init; }
    public required bool StdoutTruncated { get; init; }
    public required bool StderrTruncated { get; init; }
    public required string EvidenceClass { get; init; }
    public required IReadOnlyList<string> InvalidationReasons { get; init; }
}
