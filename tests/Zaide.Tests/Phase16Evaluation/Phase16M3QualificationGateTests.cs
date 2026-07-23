using System;
using Phase16NativeHarnessEvaluation;
using Xunit;

namespace Zaide.Tests.Phase16Evaluation;

[Collection("Phase16Isolation")]
public sealed class Phase16M3QualificationGateTests
{
    [Fact]
    public void UpstreamGate_AllowsQwenQualificationManifestWhenGrantActive()
    {
        var previous = Environment.GetEnvironmentVariable(Phase16M3QualificationPolicy.QualificationGrantEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(Phase16M3QualificationPolicy.QualificationGrantEnvVar, "1");
            var runnerConfig = Phase16TestManifestFactory.CreateRunnerConfig();
            var manifest = CreateQualificationManifest(runnerConfig);
            Phase16ManifestValidator.ValidateOrThrow(manifest, runnerConfig);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Phase16M3QualificationPolicy.QualificationGrantEnvVar, previous);
        }
    }

    [Fact]
    public void UpstreamGate_RejectsQwenQualificationManifestWithoutGrant()
    {
        var previous = Environment.GetEnvironmentVariable(Phase16M3QualificationPolicy.QualificationGrantEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(Phase16M3QualificationPolicy.QualificationGrantEnvVar, null);
            var runnerConfig = Phase16TestManifestFactory.CreateRunnerConfig();
            var manifest = CreateQualificationManifest(runnerConfig);
            var ex = Assert.Throws<ManifestValidationException>(() =>
                Phase16ManifestValidator.ValidateOrThrow(manifest, runnerConfig));
            Assert.Contains("blocked", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Phase16M3QualificationPolicy.QualificationGrantEnvVar, previous);
        }
    }

    private static Phase16Manifest CreateQualificationManifest(Phase16RunnerConfig runnerConfig)
    {
        var candidate = Phase16TestManifestFactory.CreateFakeBoundCandidateIdentity();
        return new Phase16Manifest
        {
            ManifestSchemaVersion = RunnerContractVersion.ManifestSchemaVersion,
            RunnerConfigHash = Phase16RunnerConfigHasher.ComputeHash(runnerConfig),
            FixtureHash = Phase16TestManifestFactory.CreateFixtureHash(),
            TaskId = Phase16M3QualificationPolicy.AllowedTaskId,
            ExecutionMode = CandidateExecutionMode.UpstreamCandidate,
            Candidate = new CandidateIdentity
            {
                CandidateSlug = Phase16M3QualificationPolicy.AllowedCandidateSlug,
                PublicSourceUrl = candidate.PublicSourceUrl,
                PublicSourceHead = candidate.PublicSourceHead,
                ReleaseTag = candidate.ReleaseTag,
                TagCommit = candidate.TagCommit,
                ReleaseMetadataTarget = candidate.ReleaseMetadataTarget,
                SourceRev = candidate.SourceRev,
                DistributedArtifactHash = candidate.DistributedArtifactHash,
                ChangelogProductIdentity = candidate.ChangelogProductIdentity,
                ProviderIdentity = "deepseek",
                ServiceIdentity = "https://api.deepseek.com",
                ModelIdentity = Phase16M3QualificationPolicy.AllowedModel,
                ProtocolSdkIdentity = candidate.ProtocolSdkIdentity,
            },
            FakeCandidate = Phase16TestManifestFactory.CreateEchoFakeCandidate(),
            NetworkEnabled = true,
            ProcessLaunchEnabled = true,
            UpstreamArtifactPath = "/tmp/phase16-artifacts/phase-16/artifacts/qwen-code/v0.20.1/inspect/qwen-code/bin/qwen",
        };
    }
}
