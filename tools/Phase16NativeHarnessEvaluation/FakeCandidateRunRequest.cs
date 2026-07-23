namespace Phase16NativeHarnessEvaluation;

public sealed class FakeCandidateRunRequest
{
    public required Phase16Manifest Manifest { get; init; }
    public Phase16RunnerConfig? RunnerConfig { get; init; }
}
