namespace Phase16NativeHarnessEvaluation;

public sealed class CandidateIdentity
{
    public required string CandidateSlug { get; init; }
    public required string PublicSourceUrl { get; init; }
    public required string PublicSourceHead { get; init; }
    public required string ReleaseTag { get; init; }
    public required string TagCommit { get; init; }
    public required string ReleaseMetadataTarget { get; init; }
    public required string SourceRev { get; init; }
    public required string DistributedArtifactHash { get; init; }
    public required string ChangelogProductIdentity { get; init; }
    public required string ProviderIdentity { get; init; }
    public required string ServiceIdentity { get; init; }
    public required string ModelIdentity { get; init; }
    public required string ProtocolSdkIdentity { get; init; }
}
