using System;
using Phase16NativeHarnessEvaluation;
using Xunit;

namespace Zaide.Tests.Phase16Evaluation;

[Collection("Phase16Isolation")]
public sealed class Phase16UpstreamRejectionTests
{
    private static void WithoutQualificationGrant(Action action)
    {
        var previous = Environment.GetEnvironmentVariable(Phase16M3QualificationPolicy.QualificationGrantEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(Phase16M3QualificationPolicy.QualificationGrantEnvVar, null);
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(Phase16M3QualificationPolicy.QualificationGrantEnvVar, previous);
        }
    }

    [Theory]
    [InlineData("qwen-code")]
    [InlineData("opencode")]
    [InlineData("grok-build")]
    public void UpstreamGate_RejectsBlockedM1CandidateSlugs(string blockedSlug)
    {
        WithoutQualificationGrant(() =>
        {
            var runnerConfig = Phase16TestManifestFactory.CreateRunnerConfig();
        var candidate = Phase16TestManifestFactory.CreateFakeBoundCandidateIdentity();
        var manifest = new Phase16Manifest
        {
            ManifestSchemaVersion = RunnerContractVersion.ManifestSchemaVersion,
            RunnerConfigHash = Phase16RunnerConfigHasher.ComputeHash(runnerConfig),
            FixtureHash = Phase16TestManifestFactory.CreateFixtureHash(),
            TaskId = "TC-FAKE-01",
            ExecutionMode = CandidateExecutionMode.FakeRepositoryOwned,
            Candidate = new CandidateIdentity
            {
                CandidateSlug = blockedSlug,
                PublicSourceUrl = candidate.PublicSourceUrl,
                PublicSourceHead = candidate.PublicSourceHead,
                ReleaseTag = candidate.ReleaseTag,
                TagCommit = candidate.TagCommit,
                ReleaseMetadataTarget = candidate.ReleaseMetadataTarget,
                SourceRev = candidate.SourceRev,
                DistributedArtifactHash = candidate.DistributedArtifactHash,
                ChangelogProductIdentity = candidate.ChangelogProductIdentity,
                ProviderIdentity = candidate.ProviderIdentity,
                ServiceIdentity = candidate.ServiceIdentity,
                ModelIdentity = candidate.ModelIdentity,
                ProtocolSdkIdentity = candidate.ProtocolSdkIdentity,
            },
            FakeCandidate = Phase16TestManifestFactory.CreateEchoFakeCandidate(),
            NetworkEnabled = false,
            ProcessLaunchEnabled = false,
            UpstreamArtifactPath = null,
        };

        var ex = Assert.Throws<ManifestValidationException>(() =>
            UpstreamCandidateGate.ValidateExecutionRequestOrThrow(manifest));
        Assert.Contains("blocked at M1", ex.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void UpstreamGate_RejectsUpstreamExecutionMode()
    {
        WithoutQualificationGrant(() =>
        {
        var runnerConfig = Phase16TestManifestFactory.CreateRunnerConfig();
        var manifest = Phase16TestManifestFactory.CreateValidFakeManifest(
            runnerConfig,
            Phase16TestManifestFactory.CreateFixtureHash());
        manifest = new Phase16Manifest
        {
            ManifestSchemaVersion = manifest.ManifestSchemaVersion,
            RunnerConfigHash = manifest.RunnerConfigHash,
            FixtureHash = manifest.FixtureHash,
            TaskId = manifest.TaskId,
            ExecutionMode = CandidateExecutionMode.UpstreamCandidate,
            Candidate = manifest.Candidate,
            FakeCandidate = manifest.FakeCandidate,
            NetworkEnabled = false,
            ProcessLaunchEnabled = false,
            UpstreamArtifactPath = null,
        };

        var ex = Assert.Throws<ManifestValidationException>(() =>
            UpstreamCandidateGate.ValidateExecutionRequestOrThrow(manifest));
        Assert.Contains("fake_repository_owned", ex.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void UpstreamGate_RejectsNetworkAndProcessLaunchFlags()
    {
        WithoutQualificationGrant(() =>
        {
        var runnerConfig = Phase16TestManifestFactory.CreateRunnerConfig();
        var baseManifest = Phase16TestManifestFactory.CreateValidFakeManifest(
            runnerConfig,
            Phase16TestManifestFactory.CreateFixtureHash());

        var networkManifest = new Phase16Manifest
        {
            ManifestSchemaVersion = baseManifest.ManifestSchemaVersion,
            RunnerConfigHash = baseManifest.RunnerConfigHash,
            FixtureHash = baseManifest.FixtureHash,
            TaskId = baseManifest.TaskId,
            ExecutionMode = baseManifest.ExecutionMode,
            Candidate = baseManifest.Candidate,
            FakeCandidate = baseManifest.FakeCandidate,
            NetworkEnabled = true,
            ProcessLaunchEnabled = false,
            UpstreamArtifactPath = null,
        };

        var networkEx = Assert.Throws<ManifestValidationException>(() =>
            UpstreamCandidateGate.ValidateExecutionRequestOrThrow(networkManifest));
        Assert.Contains("networkEnabled", networkEx.Message, StringComparison.Ordinal);

        var processManifest = new Phase16Manifest
        {
            ManifestSchemaVersion = baseManifest.ManifestSchemaVersion,
            RunnerConfigHash = baseManifest.RunnerConfigHash,
            FixtureHash = baseManifest.FixtureHash,
            TaskId = baseManifest.TaskId,
            ExecutionMode = baseManifest.ExecutionMode,
            Candidate = baseManifest.Candidate,
            FakeCandidate = baseManifest.FakeCandidate,
            NetworkEnabled = false,
            ProcessLaunchEnabled = true,
            UpstreamArtifactPath = null,
        };

        var processEx = Assert.Throws<ManifestValidationException>(() =>
            UpstreamCandidateGate.ValidateExecutionRequestOrThrow(processManifest));
        Assert.Contains("processLaunchEnabled", processEx.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void UpstreamGate_RejectsUpstreamArtifactPath()
    {
        WithoutQualificationGrant(() =>
        {
        var runnerConfig = Phase16TestManifestFactory.CreateRunnerConfig();
        var baseManifest = Phase16TestManifestFactory.CreateValidFakeManifest(
            runnerConfig,
            Phase16TestManifestFactory.CreateFixtureHash());
        var manifest = new Phase16Manifest
        {
            ManifestSchemaVersion = baseManifest.ManifestSchemaVersion,
            RunnerConfigHash = baseManifest.RunnerConfigHash,
            FixtureHash = baseManifest.FixtureHash,
            TaskId = baseManifest.TaskId,
            ExecutionMode = baseManifest.ExecutionMode,
            Candidate = baseManifest.Candidate,
            FakeCandidate = baseManifest.FakeCandidate,
            NetworkEnabled = false,
            ProcessLaunchEnabled = false,
            UpstreamArtifactPath = "/tmp/qwen-code-linux-x64.tar.gz",
        };

        var ex = Assert.Throws<ManifestValidationException>(() =>
            UpstreamCandidateGate.ValidateExecutionRequestOrThrow(manifest));
        Assert.Contains("upstreamArtifactPath", ex.Message, StringComparison.Ordinal);
        });
    }
}
