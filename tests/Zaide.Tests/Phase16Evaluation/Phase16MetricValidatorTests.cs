using System;
using Phase16NativeHarnessEvaluation;
using Xunit;

namespace Zaide.Tests.Phase16Evaluation;

public sealed class Phase16MetricValidatorTests
{
    [Fact]
    public void Validate_RejectsNegativeMetrics()
    {
        var metrics = new TrialMetrics
        {
            PassCount = -1,
            FailCount = 0,
            DurationMilliseconds = 0,
            TurnCount = 0,
            InputTokenEstimate = 0,
            OutputTokenEstimate = 0,
        };

        var ex = Assert.Throws<ManifestValidationException>(() => TrialMetricValidator.ValidateOrThrow(metrics));
        Assert.Contains("passCount", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MetricSnapshotFakeCandidate_ProducesDeterministicMetrics()
    {
        var runnerConfig = Phase16TestManifestFactory.CreateRunnerConfig();
        var fixtureHash = Phase16TestManifestFactory.CreateFixtureHash();
        var manifest = Phase16TestManifestFactory.CreateValidFakeManifest(runnerConfig, fixtureHash);
        manifest = new Phase16Manifest
        {
            ManifestSchemaVersion = manifest.ManifestSchemaVersion,
            RunnerConfigHash = manifest.RunnerConfigHash,
            FixtureHash = manifest.FixtureHash,
            TaskId = manifest.TaskId,
            ExecutionMode = manifest.ExecutionMode,
            Candidate = manifest.Candidate,
            FakeCandidate = new FakeCandidateIdentity
            {
                FakeCandidateId = "fake-metrics-v1",
                FakeCandidateVersion = "1.0.0",
                FakeCandidateKind = "metric_snapshot",
            },
            NetworkEnabled = manifest.NetworkEnabled,
            ProcessLaunchEnabled = manifest.ProcessLaunchEnabled,
            UpstreamArtifactPath = manifest.UpstreamArtifactPath,
        };

        var first = FakeCandidateRunner.RunOffline(new FakeCandidateRunRequest { Manifest = manifest }, runnerConfig);
        var second = FakeCandidateRunner.RunOffline(new FakeCandidateRunRequest { Manifest = manifest }, runnerConfig);

        Assert.True(TrialMetricValidator.AreDeterministicallyEqual(first.Metrics, second.Metrics));
    }
}
