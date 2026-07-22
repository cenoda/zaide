namespace Phase16NativeHarnessEvaluation;

public static class Phase16ManifestValidator
{
    public static void ValidateOrThrow(Phase16Manifest manifest, Phase16RunnerConfig runnerConfig)
    {
        if (manifest.ManifestSchemaVersion != RunnerContractVersion.ManifestSchemaVersion)
        {
            throw new ManifestValidationException(
                $"Unsupported manifestSchemaVersion {manifest.ManifestSchemaVersion}; expected {RunnerContractVersion.ManifestSchemaVersion}.");
        }

        if (runnerConfig.ManifestSchemaVersion != RunnerContractVersion.ManifestSchemaVersion)
        {
            throw new ManifestValidationException(
                $"Unsupported runner config schema version {runnerConfig.ManifestSchemaVersion}.");
        }

        Sha256DigestValidator.ValidateOrThrow(manifest.RunnerConfigHash, "runnerConfigHash");
        Sha256DigestValidator.ValidateOrThrow(manifest.FixtureHash, "fixtureHash");

        var expectedRunnerHash = Phase16RunnerConfigHasher.ComputeHash(runnerConfig);
        if (!string.Equals(manifest.RunnerConfigHash, expectedRunnerHash, StringComparison.Ordinal))
        {
            throw new ManifestValidationException("runnerConfigHash does not match canonical runner configuration.");
        }

        if (string.IsNullOrWhiteSpace(manifest.TaskId))
        {
            throw new ManifestValidationException("taskId is required.");
        }

        ValidateIdentityFields(manifest.Candidate);
        ValidateFakeIdentityFields(manifest.FakeCandidate);
        UpstreamCandidateGate.ValidateExecutionRequestOrThrow(manifest);
    }

    public static void ValidateHashBindingOrThrow(
        Phase16Manifest manifest,
        string expectedFixtureHash,
        string expectedTaskId)
    {
        if (!string.Equals(manifest.FixtureHash, expectedFixtureHash, StringComparison.Ordinal))
        {
            throw new ManifestValidationException("fixtureHash binding mismatch.");
        }

        if (!string.Equals(manifest.TaskId, expectedTaskId, StringComparison.Ordinal))
        {
            throw new ManifestValidationException("taskId binding mismatch.");
        }
    }

    private static void ValidateIdentityFields(CandidateIdentity candidate)
    {
        RequireField(candidate.CandidateSlug, "candidate.candidateSlug");
        RequireField(candidate.PublicSourceUrl, "candidate.publicSourceUrl");
        RequireField(candidate.PublicSourceHead, "candidate.publicSourceHead");
        RequireField(candidate.ReleaseTag, "candidate.releaseTag");
        RequireField(candidate.TagCommit, "candidate.tagCommit");
        RequireField(candidate.ReleaseMetadataTarget, "candidate.releaseMetadataTarget");
        RequireField(candidate.SourceRev, "candidate.sourceRev");
        RequireField(candidate.DistributedArtifactHash, "candidate.distributedArtifactHash");
        RequireField(candidate.ChangelogProductIdentity, "candidate.changelogProductIdentity");
        RequireField(candidate.ProviderIdentity, "candidate.providerIdentity");
        RequireField(candidate.ServiceIdentity, "candidate.serviceIdentity");
        RequireField(candidate.ModelIdentity, "candidate.modelIdentity");
        RequireField(candidate.ProtocolSdkIdentity, "candidate.protocolSdkIdentity");
    }

    private static void ValidateFakeIdentityFields(FakeCandidateIdentity fakeCandidate)
    {
        RequireField(fakeCandidate.FakeCandidateId, "fakeCandidate.fakeCandidateId");
        RequireField(fakeCandidate.FakeCandidateVersion, "fakeCandidate.fakeCandidateVersion");
        RequireField(fakeCandidate.FakeCandidateKind, "fakeCandidate.fakeCandidateKind");
    }

    private static void RequireField(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ManifestValidationException($"Required identity field '{fieldName}' is missing or empty.");
        }
    }
}
