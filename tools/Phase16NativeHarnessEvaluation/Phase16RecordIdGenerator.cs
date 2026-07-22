namespace Phase16NativeHarnessEvaluation;

public static class Phase16RecordIdGenerator
{
    public static string CreateStableId(
        string runnerConfigHash,
        string fixtureHash,
        string taskId,
        string fakeCandidateId,
        int sequence)
    {
        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence));
        }

        var seed =
            $"{runnerConfigHash}:{fixtureHash}:{taskId}:{fakeCandidateId}:{sequence:D8}";
        var hash = ManifestCanonicalSerializer.ComputeSha256Hex(seed);
        return $"p16-{sequence:D8}-{hash[..16]}";
    }
}
