using System;
using Phase16NativeHarnessEvaluation;
using Xunit;

namespace Zaide.Tests.Phase16Evaluation;

public sealed class Phase16M3QualificationPolicyTests
{
    [Fact]
    public void ValidateSmokeArgvOrThrow_AcceptsLockedPolicy()
    {
        var argv = new[]
        {
            "--approval-mode", "plan",
            "--model", "deepseek-v4-flash",
            "--output-format", "json",
            "--max-session-turns", "5",
            "--max-wall-time", "60s",
        };

        Phase16M3QualificationPolicy.ValidateSmokeArgvOrThrow(argv);
    }

    [Fact]
    public void ValidateSmokeArgvOrThrow_RejectsMismatchedApprovalMode()
    {
        var argv = new[]
        {
            "--approval-mode", "yolo",
            "--model", "deepseek-v4-flash",
            "--output-format", "json",
            "--max-session-turns", "5",
            "--max-wall-time", "60s",
        };

        var ex = Assert.Throws<ManifestValidationException>(() =>
            Phase16M3QualificationPolicy.ValidateSmokeArgvOrThrow(argv));
        Assert.Contains("index 1", ex.Message, StringComparison.Ordinal);
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
