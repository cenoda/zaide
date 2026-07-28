using System;
using System.Text;
using System.Text.Json;
using Xunit;
using Zaide.Features.Agents.Infrastructure.Acp;

namespace Zaide.Tests.Features.Agents.Acp.Protocol;

public sealed class Phase20ProtocolJsonRpcTests
{
    [Fact]
    public void Phase20Protocol_JsonRpcRequest_RoundTripsStringId()
    {
        var request = new AcpJsonRpcRequest
        {
            Id = AcpJsonRpcRequestId.FromString("req-1"),
            Method = AcpMethodNames.Initialize,
            Params = JsonSerializer.SerializeToElement(
                new { protocolVersion = 1 },
                AcpJsonSerializerOptionsFactory.SharedOptions),
        };

        var bytes = AcpMessageCodec.SerializeRequest(request);
        var roundTrip = AcpMessageCodec.DeserializeRequest(bytes);
        Assert.Equal("req-1", roundTrip.Id.ToString());
        Assert.Equal(AcpMethodNames.Initialize, roundTrip.Method);
    }

    [Fact]
    public void Phase20Protocol_JsonRpcResponse_ErrorPreservesCode()
    {
        var response = new AcpJsonRpcResponse
        {
            Id = AcpJsonRpcRequestId.FromNumber(7),
            Error = new AcpJsonRpcError
            {
                Code = AcpJsonRpcErrorCode.RequestCancelled,
                Message = "cancelled",
            },
        };

        var bytes = AcpMessageCodec.SerializeResponse(response);
        var roundTrip = AcpMessageCodec.DeserializeResponse(bytes);
        Assert.False(roundTrip.IsSuccess);
        Assert.Equal(AcpJsonRpcErrorCode.RequestCancelled, roundTrip.Error!.Code);
    }

    [Fact]
    public void Phase20Protocol_MessageClassifier_DistinguishesNotificationFromRequest()
    {
        var notification = Encoding.UTF8.GetBytes(
            "{\"jsonrpc\":\"2.0\",\"method\":\"session/update\",\"params\":{\"sessionId\":\"s\",\"update\":{\"sessionUpdate\":\"plan\"}}}");
        var request = Encoding.UTF8.GetBytes(
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":1}}");

        Assert.Equal(AcpJsonRpcMessageKind.Notification, AcpMessageCodec.ClassifyMessage(notification));
        Assert.Equal(AcpJsonRpcMessageKind.Request, AcpMessageCodec.ClassifyMessage(request));
    }
}
