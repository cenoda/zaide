namespace Phase16NativeHarnessEvaluation;

public static class FakeCandidateIdentityComparer
{
    public static bool AreEqual(FakeCandidateIdentity left, FakeCandidateIdentity right)
    {
        return string.Equals(left.FakeCandidateId, right.FakeCandidateId, StringComparison.Ordinal) &&
               string.Equals(left.FakeCandidateVersion, right.FakeCandidateVersion, StringComparison.Ordinal) &&
               string.Equals(left.FakeCandidateKind, right.FakeCandidateKind, StringComparison.Ordinal);
    }
}
