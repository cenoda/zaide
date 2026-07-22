using System;
using System.IO;
using Phase16NativeHarnessEvaluation;
using Xunit;

namespace Zaide.Tests.Phase16Evaluation;

public sealed class Phase16FakeRunTests
{
    [Fact]
    public void RunFakeTrial_SucceedsOfflineWithoutNetworkOrProcessLaunch()
    {
        var ledgerPath = Path.Combine(Path.GetTempPath(), $"phase16-fake-{Guid.NewGuid():N}.jsonl");
        try
        {
            var runnerConfig = Phase16TestManifestFactory.CreateRunnerConfig();
            var manifest = Phase16TestManifestFactory.CreateValidFakeManifest(
                runnerConfig,
                Phase16TestManifestFactory.CreateFixtureHash());
            var store = new Phase16RecordStore(ledgerPath);
            var runner = new Phase16EvaluationRunner(runnerConfig, store);

            var record = runner.RunFakeTrial(manifest);

            Assert.Equal("fake_repository_owned", record.ExecutionMode);
            Assert.Equal("observational", record.EvidenceClass);
            Assert.Empty(record.InvalidationReasons);
            Assert.Contains("fake=echo", record.Stdout, StringComparison.Ordinal);
            Assert.False(record.StdoutTruncated);
            Assert.Equal(manifest.FakeCandidate.FakeCandidateId, record.FakeCandidate.FakeCandidateId);
        }
        finally
        {
            if (File.Exists(ledgerPath))
            {
                File.Delete(ledgerPath);
            }
        }
    }
}
