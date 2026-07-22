namespace Phase16NativeHarnessEvaluation;

public static class Phase16RunnerConfigHasher
{
    public static string ComputeHash(Phase16RunnerConfig config)
    {
        var canonical = ManifestCanonicalSerializer.SerializeForHash(new
        {
            manifestSchemaVersion = config.ManifestSchemaVersion,
            artifactRoot = config.ArtifactRoot,
            runnerCommit = config.RunnerCommit,
            campaignLockRevision = config.CampaignLockRevision,
        });

        return ManifestCanonicalSerializer.ComputeSha256Hex(canonical);
    }
}
