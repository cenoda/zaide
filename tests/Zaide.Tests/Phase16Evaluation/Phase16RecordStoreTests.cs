using System;
using System.IO;
using Phase16NativeHarnessEvaluation;
using Xunit;

namespace Zaide.Tests.Phase16Evaluation;

public sealed class Phase16RecordStoreTests
{
    [Fact]
    public void Append_IsAppendOnlyAndPreservesStableRecordIds()
    {
        var ledgerPath = CreateTempLedgerPath();
        try
        {
            var runnerConfig = Phase16TestManifestFactory.CreateRunnerConfig();
            var manifest = Phase16TestManifestFactory.CreateValidFakeManifest(
                runnerConfig,
                Phase16TestManifestFactory.CreateFixtureHash());
            var store = new Phase16RecordStore(ledgerPath);
            var runner = new Phase16EvaluationRunner(runnerConfig, store);

            var first = runner.RunFakeTrial(manifest);
            var second = runner.RunFakeTrial(manifest);

            Assert.NotEqual(first.RecordId, second.RecordId);
            Assert.StartsWith("p16-", first.RecordId, StringComparison.Ordinal);
            Assert.Equal(2, store.Count);
            Assert.Equal(first.RecordId, store.TryGetRecord(first.RecordId)!.RecordId);
        }
        finally
        {
            DeleteLedger(ledgerPath);
        }
    }

    [Fact]
    public void Append_RejectsDuplicateRecordId()
    {
        var ledgerPath = CreateTempLedgerPath();
        try
        {
            var runnerConfig = Phase16TestManifestFactory.CreateRunnerConfig();
            var manifest = Phase16TestManifestFactory.CreateValidFakeManifest(
                runnerConfig,
                Phase16TestManifestFactory.CreateFixtureHash());
            var store = new Phase16RecordStore(ledgerPath);
            var runner = new Phase16EvaluationRunner(runnerConfig, store);
            var record = runner.RunFakeTrial(manifest);

            var duplicate = new Phase16Record
            {
                RecordId = record.RecordId,
                ManifestSchemaVersion = record.ManifestSchemaVersion,
                RunnerConfigHash = record.RunnerConfigHash,
                FixtureHash = record.FixtureHash,
                TaskId = record.TaskId,
                ExecutionMode = record.ExecutionMode,
                Candidate = record.Candidate,
                FakeCandidate = record.FakeCandidate,
                Metrics = record.Metrics,
                Stdout = record.Stdout,
                Stderr = record.Stderr,
                StdoutTruncated = record.StdoutTruncated,
                StderrTruncated = record.StderrTruncated,
                EvidenceClass = record.EvidenceClass,
                InvalidationReasons = record.InvalidationReasons,
                RecordContentHash = record.RecordContentHash,
            };

            var ex = Assert.Throws<Phase16RecordValidationException>(() => store.Append(duplicate, manifest));
            Assert.Contains("Duplicate recordId", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteLedger(ledgerPath);
        }
    }

    [Fact]
    public void RejectOverwrite_ForbidsChangingExistingRecordBody()
    {
        var ledgerPath = CreateTempLedgerPath();
        try
        {
            var runnerConfig = Phase16TestManifestFactory.CreateRunnerConfig();
            var manifest = Phase16TestManifestFactory.CreateValidFakeManifest(
                runnerConfig,
                Phase16TestManifestFactory.CreateFixtureHash());
            var store = new Phase16RecordStore(ledgerPath);
            var runner = new Phase16EvaluationRunner(runnerConfig, store);
            var record = runner.RunFakeTrial(manifest);

            var ex = Assert.Throws<Phase16RecordValidationException>(() =>
                store.RejectOverwrite(record.RecordId, "{\"recordId\":\"changed\"}"));
            Assert.Contains("Overwrite", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteLedger(ledgerPath);
        }
    }

    [Fact]
    public void Append_RejectsMalformedRecordContentHash()
    {
        var ledgerPath = CreateTempLedgerPath();
        try
        {
            var runnerConfig = Phase16TestManifestFactory.CreateRunnerConfig();
            var manifest = Phase16TestManifestFactory.CreateValidFakeManifest(
                runnerConfig,
                Phase16TestManifestFactory.CreateFixtureHash());
            var store = new Phase16RecordStore(ledgerPath);
            var runner = new Phase16EvaluationRunner(runnerConfig, store);
            var valid = runner.RunFakeTrial(manifest);

            var tampered = new Phase16Record
            {
                RecordId = Phase16RecordIdGenerator.CreateStableId(
                    manifest.RunnerConfigHash,
                    manifest.FixtureHash,
                    manifest.TaskId,
                    manifest.FakeCandidate.FakeCandidateId,
                    sequence: 99),
                ManifestSchemaVersion = valid.ManifestSchemaVersion,
                RunnerConfigHash = valid.RunnerConfigHash,
                FixtureHash = valid.FixtureHash,
                TaskId = valid.TaskId,
                ExecutionMode = valid.ExecutionMode,
                Candidate = valid.Candidate,
                FakeCandidate = valid.FakeCandidate,
                Metrics = valid.Metrics,
                Stdout = valid.Stdout,
                Stderr = valid.Stderr,
                StdoutTruncated = valid.StdoutTruncated,
                StderrTruncated = valid.StderrTruncated,
                EvidenceClass = valid.EvidenceClass,
                InvalidationReasons = valid.InvalidationReasons,
                RecordContentHash = new string('0', 64),
            };

            var ex = Assert.Throws<Phase16RecordValidationException>(() => store.Append(tampered, manifest));
            Assert.Contains("recordContentHash", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteLedger(ledgerPath);
        }
    }

    private static string CreateTempLedgerPath()
    {
        return Path.Combine(Path.GetTempPath(), $"phase16-ledger-{Guid.NewGuid():N}.jsonl");
    }

    private static void DeleteLedger(string ledgerPath)
    {
        if (File.Exists(ledgerPath))
        {
            File.Delete(ledgerPath);
        }
    }
}
