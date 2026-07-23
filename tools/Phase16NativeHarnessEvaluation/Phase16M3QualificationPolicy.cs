namespace Phase16NativeHarnessEvaluation;

public static class Phase16M3QualificationPolicy
{
    public const string QualificationGrantEnvVar = "PHASE16_M3_QUALIFICATION_GRANT";
    public const string AllowedCandidateSlug = "qwen-code";
    public const string AllowedTaskId = "TC-T01";
    public const string AllowedModel = "deepseek-v4-flash";
    public const string AllowedAuthType = "openai";
    public const string AllowedServiceUrl = "https://api.deepseek.com";
    public const string AllowedCredentialEnvVar = "DEEPSEEK_API_KEY";
    public const decimal SmokeSpendCapUsd = 1m;
    public const decimal CampaignSpendCapUsd = 3m;

    public static IReadOnlyList<string> BuildLockedSmokeArgvTail()
    {
        return
        [
            "--auth-type", AllowedAuthType,
            "--openai-base-url", AllowedServiceUrl,
            "--approval-mode", "plan",
            "--model", AllowedModel,
            "--output-format", "json",
            "--max-session-turns", "5",
            "--max-wall-time", "60s",
        ];
    }

    public static bool IsQualificationGrantActive()
    {
        var value = Environment.GetEnvironmentVariable(QualificationGrantEnvVar);
        return string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    public static void ValidateSmokeArgvOrThrow(IReadOnlyList<string> arguments)
    {
        var expected = BuildLockedSmokeArgvTail();

        if (arguments.Count < expected.Count)
        {
            throw new ManifestValidationException("M3 smoke argv is shorter than the locked policy.");
        }

        for (var index = 0; index < expected.Count; index++)
        {
            if (!string.Equals(arguments[index], expected[index], StringComparison.Ordinal))
            {
                throw new ManifestValidationException(
                    $"M3 smoke argv mismatch at index {index}: expected '{expected[index]}'.");
            }
        }
    }

    public static void ValidateSpendOrThrow(decimal observedSpendUsd, decimal priorCampaignSpendUsd)
    {
        if (observedSpendUsd < 0 || priorCampaignSpendUsd < 0)
        {
            throw new ManifestValidationException("Spend values must be non-negative.");
        }

        if (observedSpendUsd > SmokeSpendCapUsd)
        {
            throw new ManifestValidationException(
                $"M3 smoke spend {observedSpendUsd:F4} USD exceeds cap {SmokeSpendCapUsd:F2} USD.");
        }

        if (priorCampaignSpendUsd + observedSpendUsd > CampaignSpendCapUsd)
        {
            throw new ManifestValidationException(
                $"Campaign cumulative spend would exceed cap {CampaignSpendCapUsd:F2} USD.");
        }
    }
}
