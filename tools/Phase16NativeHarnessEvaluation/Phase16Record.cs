namespace Phase16NativeHarnessEvaluation;

public sealed class Phase16Record
{
    public required string RecordId { get; init; }
    public required string ManifestSchemaVersion { get; init; }
    public required string RunnerConfigHash { get; init; }
    public required string FixtureHash { get; init; }
    public required string TaskId { get; init; }
    public required string ExecutionMode { get; init; }
    public required CandidateIdentity Candidate { get; init; }
    public required FakeCandidateIdentity FakeCandidate { get; init; }
    public required TrialMetrics Metrics { get; init; }
    public required string Stdout { get; init; }
    public required string Stderr { get; init; }
    public required bool StdoutTruncated { get; init; }
    public required bool StderrTruncated { get; init; }
    public required string EvidenceClass { get; init; }
    public required IReadOnlyList<string> InvalidationReasons { get; init; }
    public required string RecordContentHash { get; init; }
}
