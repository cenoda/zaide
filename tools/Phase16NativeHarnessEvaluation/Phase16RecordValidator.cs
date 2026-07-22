using System.Text;

namespace Phase16NativeHarnessEvaluation;

public static class Phase16RecordValidator
{
    private static readonly string[] AllowedFakeCandidateKinds = ["echo", "metric_snapshot"];

    public static void ValidateStructureOrThrow(Phase16Record record)
    {
        if (string.IsNullOrWhiteSpace(record.RecordId))
        {
            throw new Phase16RecordValidationException("recordId is required.");
        }

        if (string.IsNullOrWhiteSpace(record.ManifestSchemaVersion))
        {
            throw new Phase16RecordValidationException("manifestSchemaVersion is required.");
        }

        ValidateRecordDigestOrThrow(record.RunnerConfigHash, "runnerConfigHash");
        ValidateRecordDigestOrThrow(record.FixtureHash, "fixtureHash");
        ValidateRecordDigestOrThrow(record.RecordContentHash, "recordContentHash");

        if (string.IsNullOrWhiteSpace(record.TaskId))
        {
            throw new Phase16RecordValidationException("taskId is required.");
        }

        if (string.IsNullOrWhiteSpace(record.ExecutionMode))
        {
            throw new Phase16RecordValidationException("executionMode is required.");
        }

        if (record.Candidate is null)
        {
            throw new Phase16RecordValidationException("candidate identity is required.");
        }

        if (record.FakeCandidate is null)
        {
            throw new Phase16RecordValidationException("fakeCandidate identity is required.");
        }

        ValidateIdentityFields(record.Candidate);
        ValidateFakeIdentityFields(record.FakeCandidate);
        TrialMetricValidator.ValidateOrThrow(record.Metrics);

        if (record.Stdout is null || record.Stderr is null)
        {
            throw new Phase16RecordValidationException("stdout and stderr must be present.");
        }

        if (record.InvalidationReasons is null)
        {
            throw new Phase16RecordValidationException("invalidationReasons must be present.");
        }

        if (string.IsNullOrWhiteSpace(record.EvidenceClass))
        {
            throw new Phase16RecordValidationException("evidenceClass is required.");
        }

        ValidateCaptureLimitsOrThrow(record);
        ValidateM2aInvariantsOrThrow(record);
    }

    public static void ValidateManifestBindingOrThrow(Phase16Record record, Phase16Manifest manifest)
    {
        if (!string.Equals(
                record.ManifestSchemaVersion,
                RunnerContractVersion.ManifestSchemaVersionLabel,
                StringComparison.Ordinal))
        {
            throw new Phase16RecordValidationException("Record manifestSchemaVersion does not match manifest.");
        }

        if (!string.Equals(record.ExecutionMode, "fake_repository_owned", StringComparison.Ordinal))
        {
            throw new Phase16RecordValidationException("Record executionMode does not match manifest.");
        }

        if (manifest.ExecutionMode != CandidateExecutionMode.FakeRepositoryOwned)
        {
            throw new Phase16RecordValidationException("Manifest executionMode is not fake_repository_owned.");
        }

        if (!string.Equals(record.RunnerConfigHash, manifest.RunnerConfigHash, StringComparison.Ordinal))
        {
            throw new Phase16RecordValidationException("Record runnerConfigHash does not match manifest.");
        }

        if (!string.Equals(record.FixtureHash, manifest.FixtureHash, StringComparison.Ordinal))
        {
            throw new Phase16RecordValidationException("Record fixtureHash does not match manifest.");
        }

        if (!string.Equals(record.TaskId, manifest.TaskId, StringComparison.Ordinal))
        {
            throw new Phase16RecordValidationException("Record taskId does not match manifest.");
        }

        if (!CandidateIdentityComparer.AreEqual(record.Candidate, manifest.Candidate))
        {
            throw new Phase16RecordValidationException("Record candidate identity does not match manifest.");
        }

        if (!FakeCandidateIdentityComparer.AreEqual(record.FakeCandidate, manifest.FakeCandidate))
        {
            throw new Phase16RecordValidationException("Record fakeCandidate identity does not match manifest.");
        }
    }

    private static void ValidateM2aInvariantsOrThrow(Phase16Record record)
    {
        if (!string.Equals(
                record.ManifestSchemaVersion,
                RunnerContractVersion.ManifestSchemaVersionLabel,
                StringComparison.Ordinal))
        {
            throw new Phase16RecordValidationException(
                $"Record manifestSchemaVersion must be '{RunnerContractVersion.ManifestSchemaVersionLabel}'.");
        }

        if (!string.Equals(record.ExecutionMode, "fake_repository_owned", StringComparison.Ordinal))
        {
            throw new Phase16RecordValidationException(
                "Record executionMode must be 'fake_repository_owned' at M2a.");
        }

        if (!AllowedFakeCandidateKinds.Contains(record.FakeCandidate.FakeCandidateKind, StringComparer.Ordinal))
        {
            throw new Phase16RecordValidationException(
                $"Record fakeCandidateKind '{record.FakeCandidate.FakeCandidateKind}' is not authorized at M2a.");
        }

        if (UpstreamCandidateGate.BlockedCandidateSlugs.Contains(
                record.Candidate.CandidateSlug,
                StringComparer.OrdinalIgnoreCase))
        {
            throw new Phase16RecordValidationException(
                $"Record candidateSlug '{record.Candidate.CandidateSlug}' is blocked at M1.");
        }

        if (record.InvalidationReasons.Count > 0)
        {
            throw new Phase16RecordValidationException(
                "Record invalidationReasons must be empty for valid M2a fake-run records.");
        }

        if (!string.Equals(record.EvidenceClass, "observational", StringComparison.Ordinal))
        {
            throw new Phase16RecordValidationException(
                "Record evidenceClass must be 'observational' for valid M2a fake-run records.");
        }
    }

    private static void ValidateCaptureLimitsOrThrow(Phase16Record record)
    {
        var stdoutBytes = Encoding.UTF8.GetByteCount(record.Stdout);
        var stderrBytes = Encoding.UTF8.GetByteCount(record.Stderr);

        if (stdoutBytes > CaptureLimits.MaxStdoutBytes)
        {
            throw new Phase16RecordValidationException(
                $"Record stdout exceeds {CaptureLimits.MaxStdoutBytes} bytes.");
        }

        if (stderrBytes > CaptureLimits.MaxStderrBytes)
        {
            throw new Phase16RecordValidationException(
                $"Record stderr exceeds {CaptureLimits.MaxStderrBytes} bytes.");
        }

        if (record.StdoutTruncated && stdoutBytes != CaptureLimits.MaxStdoutBytes)
        {
            throw new Phase16RecordValidationException(
                "Record stdoutTruncated requires stdout to be exactly 65536 bytes.");
        }

        if (record.StderrTruncated && stderrBytes != CaptureLimits.MaxStderrBytes)
        {
            throw new Phase16RecordValidationException(
                "Record stderrTruncated requires stderr to be exactly 65536 bytes.");
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
            throw new Phase16RecordValidationException($"Required identity field '{fieldName}' is missing or empty.");
        }
    }

    private static void ValidateRecordDigestOrThrow(string? value, string fieldName)
    {
        try
        {
            Sha256DigestValidator.ValidateOrThrow(value, fieldName);
        }
        catch (ManifestValidationException ex)
        {
            throw new Phase16RecordValidationException(ex.Message);
        }
    }
}
