namespace Phase16NativeHarnessEvaluation;

public sealed class TrialMetrics
{
    public required int PassCount { get; init; }
    public required int FailCount { get; init; }
    public required long DurationMilliseconds { get; init; }
    public required int TurnCount { get; init; }
    public required long InputTokenEstimate { get; init; }
    public required long OutputTokenEstimate { get; init; }
}
