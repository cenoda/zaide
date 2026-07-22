using System;
using Phase16NativeHarnessEvaluation;
using Xunit;

namespace Zaide.Tests.Phase16Evaluation;

public sealed class Phase16Sha256DigestValidatorTests
{
    [Fact]
    public void Validate_AcceptsLowercaseHex64()
    {
        var digest = new string('a', 64);
        Sha256DigestValidator.ValidateOrThrow(digest, "fixtureHash");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RejectsMissingOrWhitespace(string? value)
    {
        var ex = Assert.Throws<ManifestValidationException>(() =>
            Sha256DigestValidator.ValidateOrThrow(value, "runnerConfigHash"));
        Assert.Contains("runnerConfigHash", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("abcdef0123456789abcdef0123456789abcdef0123456789abcdef012345678")]
    public void Validate_RejectsIncorrectLength(string value)
    {
        var ex = Assert.Throws<ManifestValidationException>(() =>
            Sha256DigestValidator.ValidateOrThrow(value, "fixtureHash"));
        Assert.Contains("64 lowercase hexadecimal", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789")]
    [InlineData("abcdef0123456789abcdef0123456789abcdef0123456789abcdef01234567g")]
    [InlineData("abcdef0123456789abcdef0123456789abcdef0123456789abcdef01234567-")]
    public void Validate_RejectsUppercaseOrNonHex(string value)
    {
        var ex = Assert.Throws<ManifestValidationException>(() =>
            Sha256DigestValidator.ValidateOrThrow(value, "recordContentHash"));
        Assert.Contains("64 lowercase hexadecimal", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsWhitespacePaddedDigest()
    {
        var digest = new string('a', 64) + " ";
        var ex = Assert.Throws<ManifestValidationException>(() =>
            Sha256DigestValidator.ValidateOrThrow(digest, "fixtureHash"));
        Assert.Contains("64 lowercase hexadecimal", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ManifestParser_RejectsInvalidFixtureHashDigest()
    {
        const string json = """
            {
              "manifestSchemaVersion": 1,
              "runnerConfigHash": "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
              "fixtureHash": "ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789",
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
                "fakeCandidateVersion": "1.0.0",
                "fakeCandidateKind": "echo"
              }
            }
            """;

        var ex = Assert.Throws<ManifestValidationException>(() => Phase16ManifestParser.Parse(json));
        Assert.Contains("fixtureHash", ex.Message, StringComparison.Ordinal);
    }
}
