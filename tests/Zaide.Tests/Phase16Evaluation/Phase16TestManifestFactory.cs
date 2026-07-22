using System;
using System.Collections.Generic;
using Phase16NativeHarnessEvaluation;

namespace Zaide.Tests.Phase16Evaluation;

internal static class Phase16TestManifestFactory
{
    public static Phase16RunnerConfig CreateRunnerConfig(string artifactRoot = "/tmp/phase16-test")
    {
        return new Phase16RunnerConfig
        {
            ManifestSchemaVersion = RunnerContractVersion.ManifestSchemaVersion,
            ArtifactRoot = artifactRoot,
            RunnerCommit = "test-runner-commit",
            CampaignLockRevision = "m1-2026-07-23",
        };
    }

    public static CandidateIdentity CreateFakeBoundCandidateIdentity()
    {
        return new CandidateIdentity
        {
            CandidateSlug = "fake-bound-candidate",
            PublicSourceUrl = "fake://repository-owned/bound-candidate",
            PublicSourceHead = "fake-head-0000000000000000000000000000000000000000",
            ReleaseTag = "fake-v0.0.0",
            TagCommit = "fake0000000000000000000000000000000000000000",
            ReleaseMetadataTarget = "fake-target-unmapped",
            SourceRev = "fake-source-rev-unmapped",
            DistributedArtifactHash = "fake0000000000000000000000000000000000000000000000000000000000000000",
            ChangelogProductIdentity = "fake-product-unmapped",
            ProviderIdentity = "fake-provider-unmapped",
            ServiceIdentity = "fake-service-unmapped",
            ModelIdentity = "fake-model-unmapped",
            ProtocolSdkIdentity = "fake-protocol-unmapped",
        };
    }

    public static FakeCandidateIdentity CreateEchoFakeCandidate()
    {
        return new FakeCandidateIdentity
        {
            FakeCandidateId = "fake-echo-v1",
            FakeCandidateVersion = "1.0.0",
            FakeCandidateKind = "echo",
        };
    }

    public static Phase16Manifest CreateValidFakeManifest(
        Phase16RunnerConfig runnerConfig,
        string fixtureHash,
        string taskId = "TC-FAKE-01")
    {
        return new Phase16Manifest
        {
            ManifestSchemaVersion = RunnerContractVersion.ManifestSchemaVersion,
            RunnerConfigHash = Phase16RunnerConfigHasher.ComputeHash(runnerConfig),
            FixtureHash = fixtureHash,
            TaskId = taskId,
            ExecutionMode = CandidateExecutionMode.FakeRepositoryOwned,
            Candidate = CreateFakeBoundCandidateIdentity(),
            FakeCandidate = CreateEchoFakeCandidate(),
            NetworkEnabled = false,
            ProcessLaunchEnabled = false,
            UpstreamArtifactPath = null,
        };
    }

    public static string CreateFixtureHash()
    {
        return FixtureTreeHasher.ComputeHash(new Dictionary<string, string>
        {
            ["workspace/Program.cs"] = "Console.WriteLine(\"phase16\");",
        });
    }
}
