namespace Phase16NativeHarnessEvaluation;

public static class CandidateIdentityComparer
{
    public static bool AreEqual(CandidateIdentity left, CandidateIdentity right)
    {
        return string.Equals(left.CandidateSlug, right.CandidateSlug, StringComparison.Ordinal) &&
               string.Equals(left.PublicSourceUrl, right.PublicSourceUrl, StringComparison.Ordinal) &&
               string.Equals(left.PublicSourceHead, right.PublicSourceHead, StringComparison.Ordinal) &&
               string.Equals(left.ReleaseTag, right.ReleaseTag, StringComparison.Ordinal) &&
               string.Equals(left.TagCommit, right.TagCommit, StringComparison.Ordinal) &&
               string.Equals(left.ReleaseMetadataTarget, right.ReleaseMetadataTarget, StringComparison.Ordinal) &&
               string.Equals(left.SourceRev, right.SourceRev, StringComparison.Ordinal) &&
               string.Equals(left.DistributedArtifactHash, right.DistributedArtifactHash, StringComparison.Ordinal) &&
               string.Equals(left.ChangelogProductIdentity, right.ChangelogProductIdentity, StringComparison.Ordinal) &&
               string.Equals(left.ProviderIdentity, right.ProviderIdentity, StringComparison.Ordinal) &&
               string.Equals(left.ServiceIdentity, right.ServiceIdentity, StringComparison.Ordinal) &&
               string.Equals(left.ModelIdentity, right.ModelIdentity, StringComparison.Ordinal) &&
               string.Equals(left.ProtocolSdkIdentity, right.ProtocolSdkIdentity, StringComparison.Ordinal);
    }
}
