using System;
using System.IO;
using Phase16NativeHarnessEvaluation;
using Xunit;

namespace Zaide.Tests.Phase16Evaluation;

[Collection("Phase16Isolation")]
public sealed class Phase16SandboxProbeTests
{
    [Theory]
    [InlineData("probe-argv-env")]
    [InlineData("probe-writable-root")]
    [InlineData("probe-traversal")]
    [InlineData("probe-timeout")]
    [InlineData("probe-cancel")]
    [InlineData("probe-descendant")]
    [InlineData("probe-output-flood")]
    [InlineData("probe-redaction")]
    [InlineData("probe-workspace-dirty")]
    public void SandboxProbeFakeCandidate_ProducesObservationalEvidence(string probeId)
    {
        if (!Phase16SandboxAvailability.IsBubblewrapAvailable())
        {
            return;
        }

        Phase16CleanupGate.ResetForTesting();
        var artifactRoot = Phase16TestManifestFactory.CreateTempArtifactRoot();
        var ledgerPath = Path.Combine(Path.GetTempPath(), $"phase16-sandbox-{Guid.NewGuid():N}.jsonl");
        try
        {
            var runnerConfig = Phase16TestManifestFactory.CreateRunnerConfig(artifactRoot);
            var manifest = Phase16TestManifestFactory.CreateSandboxProbeManifest(
                runnerConfig,
                Phase16TestManifestFactory.CreateFixtureHash(),
                probeId);
            var store = new Phase16RecordStore(ledgerPath);
            var runner = new Phase16EvaluationRunner(runnerConfig, store);

            var record = runner.RunFakeTrial(manifest);

            Assert.Equal("sandbox_probe", record.FakeCandidate.FakeCandidateKind);
            Assert.Equal(probeId, record.FakeCandidate.FakeCandidateId);
            Assert.Equal("observational", record.EvidenceClass);
            Assert.Empty(record.InvalidationReasons);
            Assert.Contains("probe=", record.Stdout, StringComparison.Ordinal);
        }
        finally
        {
            Phase16CleanupGate.ResetForTesting();
            if (Directory.Exists(artifactRoot))
            {
                Directory.Delete(artifactRoot, recursive: true);
            }

            if (File.Exists(ledgerPath))
            {
                File.Delete(ledgerPath);
            }
        }
    }

    [Fact]
    public void RunFakeTrial_BlocksAfterCleanupFailure()
    {
        if (!Phase16SandboxAvailability.IsBubblewrapAvailable())
        {
            return;
        }

        Phase16CleanupGate.ResetForTesting();
        var artifactRoot = Phase16TestManifestFactory.CreateTempArtifactRoot();
        var ledgerPath = Path.Combine(Path.GetTempPath(), $"phase16-blocked-{Guid.NewGuid():N}.jsonl");
        try
        {
            Phase16CleanupGate.RecordCleanupFailure("simulated cleanup failure");
            var runnerConfig = Phase16TestManifestFactory.CreateRunnerConfig(artifactRoot);
            var manifest = Phase16TestManifestFactory.CreateValidFakeManifest(
                runnerConfig,
                Phase16TestManifestFactory.CreateFixtureHash());
            var store = new Phase16RecordStore(ledgerPath);
            var runner = new Phase16EvaluationRunner(runnerConfig, store);

            var ex = Assert.Throws<Phase16CleanupBlockedException>(() => runner.RunFakeTrial(manifest));
            Assert.Contains("simulated cleanup failure", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Phase16CleanupGate.ResetForTesting();
            if (Directory.Exists(artifactRoot))
            {
                Directory.Delete(artifactRoot, recursive: true);
            }

            if (File.Exists(ledgerPath))
            {
                File.Delete(ledgerPath);
            }
        }
    }
}
