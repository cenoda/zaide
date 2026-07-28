using System;
namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Pinned ACP v1 stable schema profile for Phase 20 M1.
/// </summary>
internal static class AcpSchemaProfile
{
    public const int WireProtocolVersion = 1;

    public const string SchemaArtifactVersion = "schema-v1.20.0";

    public const string SchemaCommit = "5e89c71497fe07dd4ae633c181a17224f4a8956d";

    public const string SchemaDigestSha256 =
        "92c1dfcda10dd47e99127500a3763da2b471f9ac61e12b9bf0430c32cf953796";

    public const string MetadataDigestSha256 =
        "e0bf36f8123b2544b499174197fdc371ec49a1b4572a35114513d56492741599";
}
