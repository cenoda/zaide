using System;
using System.Collections.Generic;
using Phase16NativeHarnessEvaluation;
using Xunit;

namespace Zaide.Tests.Phase16Evaluation;

public sealed class Phase16HashBindingTests
{
    [Fact]
    public void RunnerConfigHash_IsDeterministicForSameConfig()
    {
        var config = Phase16TestManifestFactory.CreateRunnerConfig("/tmp/a");
        var first = Phase16RunnerConfigHasher.ComputeHash(config);
        var second = Phase16RunnerConfigHasher.ComputeHash(config);
        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void ManifestValidator_RejectsRunnerConfigHashMismatch()
    {
        var runnerConfig = Phase16TestManifestFactory.CreateRunnerConfig();
        var manifest = Phase16TestManifestFactory.CreateValidFakeManifest(
            runnerConfig,
            Phase16TestManifestFactory.CreateFixtureHash());
        manifest = new Phase16Manifest
        {
            ManifestSchemaVersion = manifest.ManifestSchemaVersion,
            RunnerConfigHash = new string('a', 64),
            FixtureHash = manifest.FixtureHash,
            TaskId = manifest.TaskId,
            ExecutionMode = manifest.ExecutionMode,
            Candidate = manifest.Candidate,
            FakeCandidate = manifest.FakeCandidate,
            NetworkEnabled = manifest.NetworkEnabled,
            ProcessLaunchEnabled = manifest.ProcessLaunchEnabled,
            UpstreamArtifactPath = manifest.UpstreamArtifactPath,
        };

        var ex = Assert.Throws<ManifestValidationException>(() =>
            Phase16ManifestValidator.ValidateOrThrow(manifest, runnerConfig));
        Assert.Contains("runnerConfigHash", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateHashBinding_RejectsFixtureAndTaskMismatch()
    {
        var runnerConfig = Phase16TestManifestFactory.CreateRunnerConfig();
        var manifest = Phase16TestManifestFactory.CreateValidFakeManifest(
            runnerConfig,
            Phase16TestManifestFactory.CreateFixtureHash(),
            taskId: "TC-FAKE-01");

        var ex = Assert.Throws<ManifestValidationException>(() =>
            Phase16ManifestValidator.ValidateHashBindingOrThrow(
                manifest,
                expectedFixtureHash: new string('b', 64),
                expectedTaskId: "TC-FAKE-01"));
        Assert.Contains("fixtureHash", ex.Message, StringComparison.Ordinal);

        ex = Assert.Throws<ManifestValidationException>(() =>
            Phase16ManifestValidator.ValidateHashBindingOrThrow(
                manifest,
                expectedFixtureHash: manifest.FixtureHash,
                expectedTaskId: "TC-FAKE-99"));
        Assert.Contains("taskId", ex.Message, StringComparison.Ordinal);
    }
}
