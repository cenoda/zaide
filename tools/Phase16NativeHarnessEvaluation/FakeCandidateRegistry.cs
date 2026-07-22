namespace Phase16NativeHarnessEvaluation;

public static class FakeCandidateRegistry
{
    public static FakeCandidateKind ResolveKind(string fakeCandidateKind)
    {
        return fakeCandidateKind switch
        {
            "echo" => FakeCandidateKind.Echo,
            "metric_snapshot" => FakeCandidateKind.MetricSnapshot,
            _ => throw new ManifestValidationException($"Unknown fakeCandidateKind '{fakeCandidateKind}'."),
        };
    }
}
