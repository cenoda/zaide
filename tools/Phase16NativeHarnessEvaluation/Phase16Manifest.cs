namespace Phase16NativeHarnessEvaluation;

public sealed class Phase16Manifest
{
    public required int ManifestSchemaVersion { get; init; }
    public required string RunnerConfigHash { get; init; }
    public required string FixtureHash { get; init; }
    public required string TaskId { get; init; }
    public required CandidateExecutionMode ExecutionMode { get; init; }
    public required CandidateIdentity Candidate { get; init; }
    public required FakeCandidateIdentity FakeCandidate { get; init; }
    public bool NetworkEnabled { get; init; }
    public bool ProcessLaunchEnabled { get; init; }
    public string? UpstreamArtifactPath { get; init; }
}
