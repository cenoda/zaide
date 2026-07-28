using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Infrastructure.Acp;

namespace Zaide.Tests.Features.Agents.Acp.Protocol;

public sealed class Phase20ProtocolCancellationTests
{
    [Fact]
    public async Task Phase20ProtocolCancellation_CancelRequestNotification_Serializes()
    {
        await using var stream = new MemoryStream();
        var notification = new AcpJsonRpcNotification
        {
            Method = AcpMethodNames.CancelRequest,
            Params = JsonSerializer.SerializeToElement(
                new AcpCancelRequestParams { RequestId = AcpJsonRpcRequestId.FromNumber(42) },
                AcpJsonSerializerOptionsFactory.SharedOptions),
        };

        await AcpNewlineFrameWriter.WriteJsonFrameAsync(
            stream,
            AcpMessageCodec.SerializeNotification(notification),
            CancellationToken.None);

        stream.Position = 0;
        var reader = new AcpNewlineFrameReader(stream);
        var frame = await reader.ReadFrameAsync(CancellationToken.None);
        var roundTrip = AcpMessageCodec.DeserializeNotification(frame!.Value.Span);
        Assert.Equal(AcpMethodNames.CancelRequest, roundTrip.Method);
    }

    [Fact]
    public void Phase20ProtocolCancellation_StopReason_IncludesCancelled()
    {
        Assert.True(AcpStopReasonWire.TryParse(AcpStopReasonWire.Cancelled, out var reason));
        Assert.Equal(AcpStopReason.Cancelled, reason);
    }
}
