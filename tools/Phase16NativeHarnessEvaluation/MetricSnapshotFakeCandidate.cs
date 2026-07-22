namespace Phase16NativeHarnessEvaluation;

public static class MetricSnapshotFakeCandidate
{
    public static FakeCandidateRunResult Run(FakeCandidateRunRequest request)
    {
        var manifest = request.Manifest;
        var bindingSeed = $"{manifest.TaskId}|{manifest.FixtureHash}|{manifest.FakeCandidate.FakeCandidateId}";

        var metrics = new TrialMetrics
        {
            PassCount = 1,
            FailCount = 0,
            DurationMilliseconds = ComputeDeterministicLong(bindingSeed, 42),
            TurnCount = 2,
            InputTokenEstimate = ComputeDeterministicLong(bindingSeed, 128),
            OutputTokenEstimate = ComputeDeterministicLong(bindingSeed, 64),
        };

        TrialMetricValidator.ValidateOrThrow(metrics);

        var stdoutBuffer = new StreamCaptureBuffer(CaptureLimits.MaxStdoutBytes);
        stdoutBuffer.Append(
            $"metrics pass={metrics.PassCount} fail={metrics.FailCount} duration={metrics.DurationMilliseconds}");

        return new FakeCandidateRunResult
        {
            Metrics = metrics,
            Stdout = stdoutBuffer.GetCapturedText(),
            Stderr = string.Empty,
            StdoutTruncated = stdoutBuffer.Truncated,
            StderrTruncated = false,
            EvidenceClass = "observational",
            InvalidationReasons = Array.Empty<string>(),
        };
    }

    private static long ComputeDeterministicLong(string seed, long baseValue)
    {
        unchecked
        {
            long hash = 17;
            foreach (var ch in seed)
            {
                hash = (hash * 31) + ch;
            }

            return baseValue + (hash & 0xFF);
        }
    }
}
