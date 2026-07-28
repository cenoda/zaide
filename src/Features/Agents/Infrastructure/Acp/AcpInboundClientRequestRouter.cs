using System;
using System.Threading;
using System.Threading.Tasks;
namespace Zaide.Features.Agents.Infrastructure.Acp;

/// <summary>
/// Default M1 inbound client request handler with truthful unsupported-method responses.
/// </summary>
internal sealed class AcpInboundClientRequestRouter
{
    private readonly AcpClientCapabilities _advertisedCapabilities;

    public AcpInboundClientRequestRouter(AcpClientCapabilities advertisedCapabilities)
    {
        _advertisedCapabilities = advertisedCapabilities
            ?? throw new ArgumentNullException(nameof(advertisedCapabilities));
    }

    public Task<AcpJsonRpcResponse> HandleAsync(
        AcpJsonRpcRequest request,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        if (request.Method.StartsWith('_'))
        {
            return Task.FromResult(MethodNotFound(request.Id, "Custom ACP methods are not enabled."));
        }

        return request.Method switch
        {
            AcpMethodNames.TerminalCreate or
            AcpMethodNames.TerminalOutput or
            AcpMethodNames.TerminalRelease or
            AcpMethodNames.TerminalWaitForExit or
            AcpMethodNames.TerminalKill =>
                Task.FromResult(MethodNotFound(request.Id, "Terminal capability is not advertised.")),

            AcpMethodNames.FsReadTextFile when !_advertisedCapabilities.Fs.ReadTextFile =>
                Task.FromResult(MethodNotFound(request.Id, "fs.readTextFile capability is not advertised.")),

            AcpMethodNames.FsWriteTextFile when !_advertisedCapabilities.Fs.WriteTextFile =>
                Task.FromResult(MethodNotFound(request.Id, "fs.writeTextFile capability is not advertised.")),

            AcpMethodNames.FsReadTextFile or AcpMethodNames.FsWriteTextFile
                when _advertisedCapabilities.Fs.ReadTextFile || _advertisedCapabilities.Fs.WriteTextFile =>
                Task.FromResult(MethodNotFound(
                    request.Id,
                    "ACP filesystem handler is not configured.")),

            AcpMethodNames.SessionRequestPermission =>
                Task.FromResult(MethodNotFound(
                    request.Id,
                    "ACP permission handler is not configured.")),

            _ => Task.FromResult(MethodNotFound(request.Id, "ACP client method is not supported.")),
        };
    }

    private static AcpJsonRpcResponse MethodNotFound(AcpJsonRpcRequestId id, string message) =>
        new()
        {
            Id = id,
            Error = new AcpJsonRpcError
            {
                Code = AcpJsonRpcErrorCode.MethodNotFound,
                Message = message,
            },
        };
}
