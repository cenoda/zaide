namespace Phase16NativeHarnessEvaluation;

public static class UpstreamCandidateGate
{
    public static readonly string[] BlockedCandidateSlugs =
    [
        "qwen-code",
        "opencode",
        "grok-build",
    ];

    public static void ValidateExecutionRequestOrThrow(Phase16Manifest manifest)
    {
        if (manifest.ExecutionMode != CandidateExecutionMode.FakeRepositoryOwned)
        {
            throw new ManifestValidationException(
                "Only fake_repository_owned execution is authorized; upstream_candidate is blocked.");
        }

        if (manifest.NetworkEnabled)
        {
            throw new ManifestValidationException("networkEnabled must be false for repository-owned fake runs.");
        }

        if (manifest.ProcessLaunchEnabled)
        {
            throw new ManifestValidationException(
                "processLaunchEnabled must be false in manifests; sandbox launch remains runner-owned.");
        }

        if (!string.IsNullOrWhiteSpace(manifest.UpstreamArtifactPath))
        {
            throw new ManifestValidationException("upstreamArtifactPath is forbidden while M1 dispositions are blocked.");
        }

        if (BlockedCandidateSlugs.Contains(manifest.Candidate.CandidateSlug, StringComparer.OrdinalIgnoreCase))
        {
            throw new ManifestValidationException(
                $"Candidate '{manifest.Candidate.CandidateSlug}' is blocked at M1 and cannot be executed.");
        }
    }
}
