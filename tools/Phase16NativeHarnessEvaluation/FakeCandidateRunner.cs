namespace Phase16NativeHarnessEvaluation;

public static class FakeCandidateRunner
{
    public static FakeCandidateRunResult RunOffline(FakeCandidateRunRequest request, Phase16RunnerConfig runnerConfig)
    {
        Phase16ManifestValidator.ValidateOrThrow(request.Manifest, runnerConfig);
        UpstreamCandidateGate.ValidateExecutionRequestOrThrow(request.Manifest);

        var kind = FakeCandidateRegistry.ResolveKind(request.Manifest.FakeCandidate.FakeCandidateKind);
        return kind switch
        {
            FakeCandidateKind.Echo => EchoFakeCandidate.Run(request),
            FakeCandidateKind.MetricSnapshot => MetricSnapshotFakeCandidate.Run(request),
            FakeCandidateKind.SandboxProbe => SandboxProbeFakeCandidate.Run(request),
            _ => throw new ManifestValidationException($"Unsupported fake candidate kind '{kind}'."),
        };
    }
}
