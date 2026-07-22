namespace Phase16NativeHarnessEvaluation;

public static class EchoFakeCandidate
{
    public static FakeCandidateRunResult Run(FakeCandidateRunRequest request)
    {
        var manifest = request.Manifest;
        var stdoutBuffer = new StreamCaptureBuffer(CaptureLimits.MaxStdoutBytes);
        var stderrBuffer = new StreamCaptureBuffer(CaptureLimits.MaxStderrBytes);

        var line =
            $"fake=echo task={manifest.TaskId} fixture={manifest.FixtureHash} fake_id={manifest.FakeCandidate.FakeCandidateId}";
        stdoutBuffer.Append(line);

        var metrics = new TrialMetrics
        {
            PassCount = 1,
            FailCount = 0,
            DurationMilliseconds = 1,
            TurnCount = 1,
            InputTokenEstimate = line.Length,
            OutputTokenEstimate = line.Length,
        };

        TrialMetricValidator.ValidateOrThrow(metrics);

        return new FakeCandidateRunResult
        {
            Metrics = metrics,
            Stdout = stdoutBuffer.GetCapturedText(),
            Stderr = stderrBuffer.GetCapturedText(),
            StdoutTruncated = stdoutBuffer.Truncated,
            StderrTruncated = stderrBuffer.Truncated,
            EvidenceClass = "observational",
            InvalidationReasons = Array.Empty<string>(),
        };
    }
}
