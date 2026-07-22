namespace Phase16NativeHarnessEvaluation;

public static class TrialMetricValidator
{
    public static void ValidateOrThrow(TrialMetrics metrics)
    {
        if (metrics.PassCount < 0 || metrics.FailCount < 0)
        {
            throw new ManifestValidationException("Metric passCount and failCount must be non-negative.");
        }

        if (metrics.DurationMilliseconds < 0)
        {
            throw new ManifestValidationException("Metric durationMilliseconds must be non-negative.");
        }

        if (metrics.TurnCount < 0)
        {
            throw new ManifestValidationException("Metric turnCount must be non-negative.");
        }

        if (metrics.InputTokenEstimate < 0 || metrics.OutputTokenEstimate < 0)
        {
            throw new ManifestValidationException("Token estimate metrics must be non-negative.");
        }
    }

    public static bool AreDeterministicallyEqual(TrialMetrics left, TrialMetrics right)
    {
        return left.PassCount == right.PassCount &&
               left.FailCount == right.FailCount &&
               left.DurationMilliseconds == right.DurationMilliseconds &&
               left.TurnCount == right.TurnCount &&
               left.InputTokenEstimate == right.InputTokenEstimate &&
               left.OutputTokenEstimate == right.OutputTokenEstimate;
    }
}
