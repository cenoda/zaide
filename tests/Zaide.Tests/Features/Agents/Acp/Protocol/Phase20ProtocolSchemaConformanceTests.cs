using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure.Acp;
using Zaide.Tests.Architecture;

namespace Zaide.Tests.Features.Agents.Acp.Protocol;

public sealed class Phase20ProtocolSchemaConformanceTests
{
    private static readonly string RepositoryRoot = ArchitectureInventoryReader.ResolveRepositoryRoot();

    private static string FixturePath(string fileName) =>
        Path.Combine(RepositoryRoot, "tests/Zaide.Tests/Features/Agents/Acp/Protocol/Fixtures", fileName);

    [Fact]
    public void Phase20Protocol_SchemaFixture_MatchesPinnedDigest()
    {
        var bytes = File.ReadAllBytes(FixturePath("schema-v1.20.0.json"));
        var digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Assert.Equal(AcpSchemaProfile.SchemaDigestSha256, digest);
    }

    [Fact]
    public void Phase20Protocol_MetaFixture_MatchesPinnedDigestAndMethods()
    {
        var bytes = File.ReadAllBytes(FixturePath("meta-v1.20.0.json"));
        var digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Assert.Equal(AcpSchemaProfile.MetadataDigestSha256, digest);

        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        Assert.Equal(AcpSchemaProfile.WireProtocolVersion, root.GetProperty("version").GetInt32());
        Assert.Equal(AcpMethodNames.Initialize, root.GetProperty("agentMethods").GetProperty("initialize").GetString());
        Assert.Equal(AcpMethodNames.SessionUpdate, root.GetProperty("clientMethods").GetProperty("session_update").GetString());
        Assert.Equal(AcpMethodNames.CancelRequest, root.GetProperty("protocolMethods").GetProperty("cancel_request").GetString());
    }

    [Fact]
    public void Phase20Protocol_SchemaProfile_LocksArtifactVersion()
    {
        Assert.Equal("schema-v1.20.0", AcpSchemaProfile.SchemaArtifactVersion);
        Assert.Equal("5e89c71497fe07dd4ae633c181a17224f4a8956d", AcpSchemaProfile.SchemaCommit);
        Assert.Equal(1, AcpSchemaProfile.WireProtocolVersion);
    }
}
