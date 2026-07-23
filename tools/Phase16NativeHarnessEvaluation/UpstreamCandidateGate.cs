namespace Phase16NativeHarnessEvaluation;

public static class UpstreamCandidateGate
{
    public static readonly string[] AlwaysBlockedCandidateSlugs =
    [
        "opencode",
        "grok-build",
    ];

    public static void ValidateExecutionRequestOrThrow(Phase16Manifest manifest)
    {
        if (Phase16M3QualificationPolicy.IsQualificationGrantActive()
            && manifest.ExecutionMode == CandidateExecutionMode.UpstreamCandidate
            && string.Equals(
                manifest.Candidate.CandidateSlug,
                Phase16M3QualificationPolicy.AllowedCandidateSlug,
                StringComparison.OrdinalIgnoreCase))
        {
            ValidateM3QualificationManifestOrThrow(manifest);
            return;
        }

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

        if (IsBlockedAtM1(manifest.Candidate.CandidateSlug))
        {
            throw new ManifestValidationException(
                $"Candidate '{manifest.Candidate.CandidateSlug}' is blocked at M1 and cannot be executed.");
        }
    }

    public static bool IsBlockedAtM1(string candidateSlug)
    {
        if (string.Equals(candidateSlug, Phase16M3QualificationPolicy.AllowedCandidateSlug, StringComparison.OrdinalIgnoreCase))
        {
            return !Phase16M3QualificationPolicy.IsQualificationGrantActive();
        }

        return AlwaysBlockedCandidateSlugs.Contains(candidateSlug, StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateM3QualificationManifestOrThrow(Phase16Manifest manifest)
    {
        if (!string.Equals(
                manifest.Candidate.CandidateSlug,
                Phase16M3QualificationPolicy.AllowedCandidateSlug,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ManifestValidationException(
                "M3 qualification grant permits only the qwen-code candidate slug.");
        }

        if (!string.Equals(manifest.TaskId, Phase16M3QualificationPolicy.AllowedTaskId, StringComparison.Ordinal))
        {
            throw new ManifestValidationException(
                $"M3 qualification grant permits only task id {Phase16M3QualificationPolicy.AllowedTaskId}.");
        }

        if (!manifest.NetworkEnabled)
        {
            throw new ManifestValidationException("networkEnabled must be true for M3 qualification smoke.");
        }

        if (!manifest.ProcessLaunchEnabled)
        {
            throw new ManifestValidationException("processLaunchEnabled must be true for M3 qualification smoke.");
        }

        if (string.IsNullOrWhiteSpace(manifest.UpstreamArtifactPath))
        {
            throw new ManifestValidationException("upstreamArtifactPath is required for M3 qualification smoke.");
        }
    }
}
