using System;
using System.Linq;
using Phase16NativeHarnessEvaluation;
using Xunit;

namespace Zaide.Tests.Phase16Evaluation;

public sealed class Phase16M3QualificationPolicyTests
{
    [Fact]
    public void BuildLockedSmokeArgvTail_IncludesDeepSeekOpenAiCompatibleAuth()
    {
        var argv = Phase16M3QualificationPolicy.BuildLockedSmokeArgvTail().ToArray();

        Assert.Equal("openai", argv[Array.IndexOf(argv, "--auth-type") + 1]);
        Assert.Equal(
            "https://api.deepseek.com",
            argv[Array.IndexOf(argv, "--openai-base-url") + 1]);
        Assert.Equal("deepseek-v4-flash", argv[Array.IndexOf(argv, "--model") + 1]);
    }

    [Fact]
    public void ValidateSmokeArgvOrThrow_AcceptsLockedPolicy()
    {
        var argv = Phase16M3QualificationPolicy.BuildLockedSmokeArgvTail().ToArray();

        Phase16M3QualificationPolicy.ValidateSmokeArgvOrThrow(argv);
    }

    [Fact]
    public void ValidateSmokeArgvOrThrow_RejectsMissingAuthType()
    {
        var argv = Phase16M3QualificationPolicy.BuildLockedSmokeArgvTail()
            .Skip(2)
            .ToArray();

        var ex = Assert.Throws<ManifestValidationException>(() =>
            Phase16M3QualificationPolicy.ValidateSmokeArgvOrThrow(argv));
        Assert.Contains("shorter than the locked policy", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateSmokeArgvOrThrow_RejectsMismatchedApprovalMode()
    {
        var argv = Phase16M3QualificationPolicy.BuildLockedSmokeArgvTail()
            .Select((value, index) => index == 5 ? "yolo" : value)
            .ToArray();

        var ex = Assert.Throws<ManifestValidationException>(() =>
            Phase16M3QualificationPolicy.ValidateSmokeArgvOrThrow(argv));
        Assert.Contains("index 5", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateSpendOrThrow_EnforcesSmokeAndCampaignCaps()
    {
        Phase16M3QualificationPolicy.ValidateSpendOrThrow(0.50m, 2.00m);

        Assert.Throws<ManifestValidationException>(() =>
            Phase16M3QualificationPolicy.ValidateSpendOrThrow(1.50m, 0m));

        Assert.Throws<ManifestValidationException>(() =>
            Phase16M3QualificationPolicy.ValidateSpendOrThrow(0.50m, 2.75m));
    }
}
