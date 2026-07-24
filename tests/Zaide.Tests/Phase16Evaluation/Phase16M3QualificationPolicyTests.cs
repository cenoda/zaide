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
        Assert.Equal(
            Phase16M3QualificationPolicy.AllowedApprovalMode,
            argv[Array.IndexOf(argv, "--approval-mode") + 1]);
        Assert.Equal("yolo", argv[Array.IndexOf(argv, "--approval-mode") + 1]);
        Assert.Equal("deepseek-v4-flash", argv[Array.IndexOf(argv, "--model") + 1]);
        Assert.Equal(
            Phase16M3QualificationPolicy.MaxSessionTurns.ToString(),
            argv[Array.IndexOf(argv, "--max-session-turns") + 1]);
        Assert.Equal(
            Phase16M3QualificationPolicy.MaxWallTime,
            argv[Array.IndexOf(argv, "--max-wall-time") + 1]);
        Assert.Equal(12, Phase16M3QualificationPolicy.MaxSessionTurns);
        Assert.Equal("120s", Phase16M3QualificationPolicy.MaxWallTime);
        Assert.Equal(1m, Phase16M3QualificationPolicy.SmokeSpendCapUsd);
        Assert.Equal(3m, Phase16M3QualificationPolicy.CampaignSpendCapUsd);
        Assert.DoesNotContain("--yolo", argv);
        Assert.DoesNotContain("-y", argv);
        Assert.DoesNotContain("plan", argv);
        Assert.DoesNotContain("60s", argv);
    }

    [Fact]
    public void ValidateSmokeArgvOrThrow_RejectsLegacyFiveTurnCeiling()
    {
        var locked = Phase16M3QualificationPolicy.BuildLockedSmokeArgvTail().ToArray();
        var turnValueIndex = Array.IndexOf(locked, "--max-session-turns") + 1;
        locked[turnValueIndex] = "5";

        var ex = Assert.Throws<ManifestValidationException>(() =>
            Phase16M3QualificationPolicy.ValidateSmokeArgvOrThrow(locked));
        Assert.Contains("expected '12'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateSmokeArgvOrThrow_RejectsLegacySixtySecondWallTime()
    {
        var locked = Phase16M3QualificationPolicy.BuildLockedSmokeArgvTail().ToArray();
        var wallValueIndex = Array.IndexOf(locked, "--max-wall-time") + 1;
        locked[wallValueIndex] = "60s";

        var ex = Assert.Throws<ManifestValidationException>(() =>
            Phase16M3QualificationPolicy.ValidateSmokeArgvOrThrow(locked));
        Assert.Contains("expected '120s'", ex.Message, StringComparison.Ordinal);
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
    public void ValidateSmokeArgvOrThrow_RejectsLegacyPlanApprovalMode()
    {
        var argv = Phase16M3QualificationPolicy.BuildLockedSmokeArgvTail()
            .Select((value, index) => index == 5 ? "plan" : value)
            .ToArray();

        var ex = Assert.Throws<ManifestValidationException>(() =>
            Phase16M3QualificationPolicy.ValidateSmokeArgvOrThrow(argv));
        Assert.Contains("index 5", ex.Message, StringComparison.Ordinal);
        Assert.Contains("expected 'yolo'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateSmokeArgvOrThrow_RejectsMismatchedApprovalMode()
    {
        var argv = Phase16M3QualificationPolicy.BuildLockedSmokeArgvTail()
            .Select((value, index) => index == 5 ? "auto-edit" : value)
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
