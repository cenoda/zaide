namespace Phase16NativeHarnessEvaluation;

public sealed class Phase16EvaluationRunner
{
    private readonly Phase16RunnerConfig _runnerConfig;
    private readonly Phase16RecordStore _recordStore;

    public Phase16EvaluationRunner(Phase16RunnerConfig runnerConfig, Phase16RecordStore recordStore)
    {
        _runnerConfig = runnerConfig;
        _recordStore = recordStore;
    }

    public Phase16Record RunFakeTrial(Phase16Manifest manifest)
    {
        Phase16CleanupGate.EnsureNotBlockedOrThrow();
        Phase16ManifestValidator.ValidateOrThrow(manifest, _runnerConfig);
        UpstreamCandidateGate.ValidateExecutionRequestOrThrow(manifest);

        var fakeResult = FakeCandidateRunner.RunOffline(
            new FakeCandidateRunRequest
            {
                Manifest = manifest,
                RunnerConfig = _runnerConfig,
            },
            _runnerConfig);

        var sequence = _recordStore.Count;
        var recordId = Phase16RecordIdGenerator.CreateStableId(
            manifest.RunnerConfigHash,
            manifest.FixtureHash,
            manifest.TaskId,
            manifest.FakeCandidate.FakeCandidateId,
            sequence);

        var recordWithoutHash = new Phase16Record
        {
            RecordId = recordId,
            ManifestSchemaVersion = RunnerContractVersion.ManifestSchemaVersionLabel,
            RunnerConfigHash = manifest.RunnerConfigHash,
            FixtureHash = manifest.FixtureHash,
            TaskId = manifest.TaskId,
            ExecutionMode = "fake_repository_owned",
            Candidate = manifest.Candidate,
            FakeCandidate = manifest.FakeCandidate,
            Metrics = fakeResult.Metrics,
            Stdout = fakeResult.Stdout,
            Stderr = fakeResult.Stderr,
            StdoutTruncated = fakeResult.StdoutTruncated,
            StderrTruncated = fakeResult.StderrTruncated,
            EvidenceClass = fakeResult.EvidenceClass,
            InvalidationReasons = fakeResult.InvalidationReasons,
            RecordContentHash = string.Empty,
        };

        var contentHash = Phase16RecordStore.ComputeRecordContentHash(recordWithoutHash);
        var record = new Phase16Record
        {
            RecordId = recordWithoutHash.RecordId,
            ManifestSchemaVersion = recordWithoutHash.ManifestSchemaVersion,
            RunnerConfigHash = recordWithoutHash.RunnerConfigHash,
            FixtureHash = recordWithoutHash.FixtureHash,
            TaskId = recordWithoutHash.TaskId,
            ExecutionMode = recordWithoutHash.ExecutionMode,
            Candidate = recordWithoutHash.Candidate,
            FakeCandidate = recordWithoutHash.FakeCandidate,
            Metrics = recordWithoutHash.Metrics,
            Stdout = recordWithoutHash.Stdout,
            Stderr = recordWithoutHash.Stderr,
            StdoutTruncated = recordWithoutHash.StdoutTruncated,
            StderrTruncated = recordWithoutHash.StderrTruncated,
            EvidenceClass = recordWithoutHash.EvidenceClass,
            InvalidationReasons = recordWithoutHash.InvalidationReasons,
            RecordContentHash = contentHash,
        };

        _recordStore.Append(record, manifest);
        return record;
    }
}
