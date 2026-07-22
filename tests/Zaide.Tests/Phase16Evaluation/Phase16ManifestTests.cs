using System;
using Phase16NativeHarnessEvaluation;
using Xunit;

namespace Zaide.Tests.Phase16Evaluation;

public sealed class Phase16ManifestTests
{
    [Fact]
    public void Parse_RejectsMissingRequiredField()
    {
        const string json = """
            {
              "manifestSchemaVersion": 1,
              "runnerConfigHash": "abc",
              "fixtureHash": "def",
              "taskId": "TC-FAKE-01",
              "executionMode": "fake_repository_owned",
              "candidate": {
                "candidateSlug": "fake-bound-candidate",
                "publicSourceUrl": "fake://repository-owned/bound-candidate",
                "publicSourceHead": "fake-head",
                "releaseTag": "fake-v0.0.0",
                "tagCommit": "fake-tag",
                "releaseMetadataTarget": "fake-target",
                "sourceRev": "fake-rev",
                "distributedArtifactHash": "fake-artifact",
                "changelogProductIdentity": "fake-product",
                "providerIdentity": "fake-provider",
                "serviceIdentity": "fake-service",
                "modelIdentity": "fake-model",
                "protocolSdkIdentity": "fake-protocol"
              },
              "fakeCandidate": {
                "fakeCandidateId": "fake-echo-v1",
                "fakeCandidateVersion": "1.0.0"
              }
            }
            """;

        var ex = Assert.Throws<ManifestValidationException>(() => Phase16ManifestParser.Parse(json));
        Assert.Contains("fakeCandidateKind", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsUnsupportedSchemaVersion()
    {
        var runnerConfig = Phase16TestManifestFactory.CreateRunnerConfig();
        var baseManifest = Phase16TestManifestFactory.CreateValidFakeManifest(
            runnerConfig,
            Phase16TestManifestFactory.CreateFixtureHash());
        var manifest = new Phase16Manifest
        {
            ManifestSchemaVersion = 99,
            RunnerConfigHash = baseManifest.RunnerConfigHash,
            FixtureHash = baseManifest.FixtureHash,
            TaskId = baseManifest.TaskId,
            ExecutionMode = baseManifest.ExecutionMode,
            Candidate = baseManifest.Candidate,
            FakeCandidate = baseManifest.FakeCandidate,
            NetworkEnabled = baseManifest.NetworkEnabled,
            ProcessLaunchEnabled = baseManifest.ProcessLaunchEnabled,
            UpstreamArtifactPath = baseManifest.UpstreamArtifactPath,
        };

        var ex = Assert.Throws<ManifestValidationException>(() =>
            Phase16ManifestValidator.ValidateOrThrow(manifest, runnerConfig));
        Assert.Contains("manifestSchemaVersion", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsEmptyIdentityField()
    {
        var runnerConfig = Phase16TestManifestFactory.CreateRunnerConfig();
        var baseManifest = Phase16TestManifestFactory.CreateValidFakeManifest(
            runnerConfig,
            Phase16TestManifestFactory.CreateFixtureHash());
        var candidate = baseManifest.Candidate;
        var manifest = new Phase16Manifest
        {
            ManifestSchemaVersion = baseManifest.ManifestSchemaVersion,
            RunnerConfigHash = baseManifest.RunnerConfigHash,
            FixtureHash = baseManifest.FixtureHash,
            TaskId = baseManifest.TaskId,
            ExecutionMode = baseManifest.ExecutionMode,
            Candidate = new CandidateIdentity
            {
                CandidateSlug = candidate.CandidateSlug,
                PublicSourceUrl = candidate.PublicSourceUrl,
                PublicSourceHead = candidate.PublicSourceHead,
                ReleaseTag = candidate.ReleaseTag,
                TagCommit = candidate.TagCommit,
                ReleaseMetadataTarget = candidate.ReleaseMetadataTarget,
                SourceRev = candidate.SourceRev,
                DistributedArtifactHash = candidate.DistributedArtifactHash,
                ChangelogProductIdentity = candidate.ChangelogProductIdentity,
                ProviderIdentity = "   ",
                ServiceIdentity = candidate.ServiceIdentity,
                ModelIdentity = candidate.ModelIdentity,
                ProtocolSdkIdentity = candidate.ProtocolSdkIdentity,
            },
            FakeCandidate = baseManifest.FakeCandidate,
            NetworkEnabled = baseManifest.NetworkEnabled,
            ProcessLaunchEnabled = baseManifest.ProcessLaunchEnabled,
            UpstreamArtifactPath = baseManifest.UpstreamArtifactPath,
        };

        var ex = Assert.Throws<ManifestValidationException>(() =>
            Phase16ManifestValidator.ValidateOrThrow(manifest, runnerConfig));
        Assert.Contains("providerIdentity", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
