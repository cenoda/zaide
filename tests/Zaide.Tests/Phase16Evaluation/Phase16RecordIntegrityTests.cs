using System;
using System.IO;
using System.Text.Json;
using Phase16NativeHarnessEvaluation;
using Xunit;

namespace Zaide.Tests.Phase16Evaluation;

public sealed class Phase16RecordIntegrityTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    [Fact]
    public void Append_RejectsCandidateIdentityMismatch()
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

            var mismatchedCandidate = new CandidateIdentity
            {
                CandidateSlug = "different-slug",
                PublicSourceUrl = record.Candidate.PublicSourceUrl,
                PublicSourceHead = record.Candidate.PublicSourceHead,
                ReleaseTag = record.Candidate.ReleaseTag,
                TagCommit = record.Candidate.TagCommit,
                ReleaseMetadataTarget = record.Candidate.ReleaseMetadataTarget,
                SourceRev = record.Candidate.SourceRev,
                DistributedArtifactHash = record.Candidate.DistributedArtifactHash,
                ChangelogProductIdentity = record.Candidate.ChangelogProductIdentity,
                ProviderIdentity = record.Candidate.ProviderIdentity,
                ServiceIdentity = record.Candidate.ServiceIdentity,
                ModelIdentity = record.Candidate.ModelIdentity,
                ProtocolSdkIdentity = record.Candidate.ProtocolSdkIdentity,
            };

            var tampered = CloneRecord(record, candidate: mismatchedCandidate);
            var ex = Assert.Throws<Phase16RecordValidationException>(() => store.Append(tampered, manifest));
            Assert.Contains("candidate identity", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteLedger(ledgerPath);
        }
    }

    [Fact]
    public void Append_RejectsExecutionModeMismatch()
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

            var tampered = CloneRecord(record, executionMode: "upstream_candidate");
            var ex = Assert.Throws<Phase16RecordValidationException>(() => store.Append(tampered, manifest));
            Assert.Contains("executionMode", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteLedger(ledgerPath);
        }
    }

    [Fact]
    public void Append_RejectsUppercaseRecordContentHash()
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

            var uppercaseHash = record.RecordContentHash.ToUpperInvariant();
            var tampered = CloneRecord(record, recordContentHash: uppercaseHash);
            var ex = Assert.Throws<Phase16RecordValidationException>(() => store.Append(tampered, manifest));
            Assert.Contains("recordContentHash", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteLedger(ledgerPath);
        }
    }

    [Fact]
    public void Reload_RejectsRehashedEvidenceClassTamper()
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

            var tampered = CloneRecord(record, evidenceClass: "invalid");
            tampered = CloneRecord(tampered, recordContentHash: Phase16RecordStore.ComputeRecordContentHash(tampered));
            File.WriteAllText(ledgerPath, JsonSerializer.Serialize(tampered, JsonOptions) + Environment.NewLine);

            var ex = Assert.Throws<Phase16RecordValidationException>(() => _ = new Phase16RecordStore(ledgerPath));
            Assert.Contains("evidenceClass", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteLedger(ledgerPath);
        }
    }

    [Fact]
    public void Reload_RejectsRehashedBlockedSlugTamper()
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

            var blockedCandidate = new CandidateIdentity
            {
                CandidateSlug = "qwen-code",
                PublicSourceUrl = record.Candidate.PublicSourceUrl,
                PublicSourceHead = record.Candidate.PublicSourceHead,
                ReleaseTag = record.Candidate.ReleaseTag,
                TagCommit = record.Candidate.TagCommit,
                ReleaseMetadataTarget = record.Candidate.ReleaseMetadataTarget,
                SourceRev = record.Candidate.SourceRev,
                DistributedArtifactHash = record.Candidate.DistributedArtifactHash,
                ChangelogProductIdentity = record.Candidate.ChangelogProductIdentity,
                ProviderIdentity = record.Candidate.ProviderIdentity,
                ServiceIdentity = record.Candidate.ServiceIdentity,
                ModelIdentity = record.Candidate.ModelIdentity,
                ProtocolSdkIdentity = record.Candidate.ProtocolSdkIdentity,
            };

            var tampered = CloneRecord(record, candidate: blockedCandidate);
            tampered = CloneRecord(tampered, recordContentHash: Phase16RecordStore.ComputeRecordContentHash(tampered));
            File.WriteAllText(ledgerPath, JsonSerializer.Serialize(tampered, JsonOptions) + Environment.NewLine);

            var ex = Assert.Throws<Phase16RecordValidationException>(() => _ = new Phase16RecordStore(ledgerPath));
            Assert.Contains("blocked at M1", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteLedger(ledgerPath);
        }
    }

    [Fact]
    public void Reload_RejectsRehashedTruncationFlagTamper()
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

            var tampered = CloneRecord(record, stdoutTruncated: true);
            tampered = CloneRecord(tampered, recordContentHash: Phase16RecordStore.ComputeRecordContentHash(tampered));
            File.WriteAllText(ledgerPath, JsonSerializer.Serialize(tampered, JsonOptions) + Environment.NewLine);

            var ex = Assert.Throws<Phase16RecordValidationException>(() => _ = new Phase16RecordStore(ledgerPath));
            Assert.Contains("stdoutTruncated", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteLedger(ledgerPath);
        }
    }

    private static Phase16Record CloneRecord(
        Phase16Record source,
        CandidateIdentity? candidate = null,
        string? executionMode = null,
        string? evidenceClass = null,
        bool? stdoutTruncated = null,
        string? recordContentHash = null)
    {
        return new Phase16Record
        {
            RecordId = source.RecordId,
            ManifestSchemaVersion = source.ManifestSchemaVersion,
            RunnerConfigHash = source.RunnerConfigHash,
            FixtureHash = source.FixtureHash,
            TaskId = source.TaskId,
            ExecutionMode = executionMode ?? source.ExecutionMode,
            Candidate = candidate ?? source.Candidate,
            FakeCandidate = source.FakeCandidate,
            Metrics = source.Metrics,
            Stdout = source.Stdout,
            Stderr = source.Stderr,
            StdoutTruncated = stdoutTruncated ?? source.StdoutTruncated,
            StderrTruncated = source.StderrTruncated,
            EvidenceClass = evidenceClass ?? source.EvidenceClass,
            InvalidationReasons = source.InvalidationReasons,
            RecordContentHash = recordContentHash ?? source.RecordContentHash,
        };
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
