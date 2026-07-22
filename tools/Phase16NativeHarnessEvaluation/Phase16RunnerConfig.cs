namespace Phase16NativeHarnessEvaluation;

public sealed class Phase16RunnerConfig
{
    public required int ManifestSchemaVersion { get; init; }
    public required string ArtifactRoot { get; init; }
    public required string RunnerCommit { get; init; }
    public required string CampaignLockRevision { get; init; }
}
