using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Infrastructure.Acp;

namespace Zaide.Tests.Features.Agents.Acp.Protocol;

public sealed class Phase20ProtocolFramingTests
{
    [Fact]
    public async Task Phase20Protocol_NewlineFraming_RejectsEmbeddedNewlines()
    {
        await using var stream = new MemoryStream();
        var payload = "{\"jsonrpc\":\"2.0\"}\n"u8.ToArray();
        await Assert.ThrowsAsync<AcpProtocolException>(() =>
            AcpNewlineFrameWriter.WriteJsonFrameAsync(stream, payload, CancellationToken.None));
    }

    [Fact]
    public async Task Phase20Protocol_NewlineFraming_ReadsSingleFrame()
    {
        await using var stream = new MemoryStream();
        var json = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":1}}";
        await AcpNewlineFrameWriter.WriteJsonFrameAsync(stream, Encoding.UTF8.GetBytes(json), CancellationToken.None);
        stream.Position = 0;

        var reader = new AcpNewlineFrameReader(stream);
        var frame = await reader.ReadFrameAsync(CancellationToken.None);
        Assert.NotNull(frame);
        Assert.Equal(json, Encoding.UTF8.GetString(frame!.Value.Span));
    }

    [Fact]
    public void Phase20Protocol_FrameValidation_RejectsOversizedPayload()
    {
        var oversized = new byte[AcpProtocolLimits.MaxFrameBytes + 1];
        Assert.Throws<AcpProtocolException>(() => AcpMessageCodec.ValidateUtf8Frame(oversized));
    }
}
