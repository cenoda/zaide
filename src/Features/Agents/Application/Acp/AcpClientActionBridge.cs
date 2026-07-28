using System;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure.Acp;

namespace Zaide.Features.Agents.Application.Acp;

/// <summary>
/// Mediates ACP client filesystem actions through the Phase 17 run-scoped broker.
/// </summary>
internal sealed class AcpClientActionBridge
{
    private readonly IAgentActionBroker _broker;
    private readonly string _workspaceRoot;
    private readonly string _expectedSessionId;
    private readonly AcpClientPermissionBridge _permissionBridge;

    public AcpClientActionBridge(
        IAgentActionBroker broker,
        string workspaceRoot,
        string expectedSessionId,
        IAcpPermissionChoiceSource? permissionChoiceSource = null)
    {
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new ArgumentException("Workspace root is required.", nameof(workspaceRoot));
        }

        _workspaceRoot = workspaceRoot;
        _expectedSessionId = expectedSessionId
            ?? throw new ArgumentNullException(nameof(expectedSessionId));
        _permissionBridge = new AcpClientPermissionBridge(expectedSessionId, permissionChoiceSource);
    }

    public bool IsAvailable => _broker is not UnavailableAgentActionBroker;

    public AcpInboundClientRequestHandler CreateInboundHandler(
        AcpInboundClientRequestRouter fallbackRouter)
    {
        ArgumentNullException.ThrowIfNull(fallbackRouter);

        return async (request, cancellationToken) =>
        {
            if (!IsAvailable)
            {
                return await fallbackRouter.HandleAsync(request, cancellationToken).ConfigureAwait(false);
            }

            return request.Method switch
            {
                AcpMethodNames.FsReadTextFile =>
                    await HandleReadAsync(request, cancellationToken).ConfigureAwait(false),
                AcpMethodNames.FsWriteTextFile =>
                    await HandleWriteAsync(request, cancellationToken).ConfigureAwait(false),
                AcpMethodNames.SessionRequestPermission =>
                    await _permissionBridge.HandleAsync(request, cancellationToken).ConfigureAwait(false),
                _ => await fallbackRouter.HandleAsync(request, cancellationToken).ConfigureAwait(false),
            };
        };
    }

    private async Task<AcpJsonRpcResponse> HandleReadAsync(
        AcpJsonRpcRequest request,
        CancellationToken cancellationToken)
    {
        AcpReadTextFileRequestWire wire;
        try
        {
            wire = AcpMessageCodec.DeserializeParams<AcpReadTextFileRequestWire>(request.Params);
        }
        catch (AcpProtocolException ex)
        {
            return InvalidParams(request.Id, ex.Message);
        }

        if (!string.Equals(wire.SessionId, _expectedSessionId, StringComparison.Ordinal))
        {
            return InvalidParams(request.Id, "ACP read request session id mismatch.");
        }

        if (wire.Line is not null || wire.Limit is not null)
        {
            return InvalidParams(request.Id, "Bounded full-file reads are supported in Phase 20 M4.");
        }

        if (!AcpWorkspaceAbsolutePathConverter.TryConvert(
                wire.Path,
                _workspaceRoot,
                out var relativePath,
                out var pathFailure))
        {
            return InvalidParams(request.Id, pathFailure ?? "ACP read path is invalid.");
        }

        var payload = new AgentReadFileActionPayload(relativePath!);
        var result = await _broker.RequestAsync(
            payload,
            correlationKey: BuildCorrelationKey("read", wire.Path),
            cancellationToken).ConfigureAwait(false);

        return MapReadResult(request.Id, result);
    }

    private async Task<AcpJsonRpcResponse> HandleWriteAsync(
        AcpJsonRpcRequest request,
        CancellationToken cancellationToken)
    {
        AcpWriteTextFileRequestWire wire;
        try
        {
            wire = AcpMessageCodec.DeserializeParams<AcpWriteTextFileRequestWire>(request.Params);
        }
        catch (AcpProtocolException ex)
        {
            return InvalidParams(request.Id, ex.Message);
        }

        if (!string.Equals(wire.SessionId, _expectedSessionId, StringComparison.Ordinal))
        {
            return InvalidParams(request.Id, "ACP write request session id mismatch.");
        }

        if (!AcpWorkspaceAbsolutePathConverter.TryConvert(
                wire.Path,
                _workspaceRoot,
                out var relativePath,
                out var pathFailure))
        {
            return InvalidParams(request.Id, pathFailure ?? "ACP write path is invalid.");
        }

        AgentActionPayload writePayload;
        try
        {
            writePayload = await ComposeWritePayloadAsync(
                relativePath!,
                wire.Content,
                cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return InvalidParams(request.Id, ex.Message);
        }

        var result = await _broker.RequestAsync(
            writePayload,
            correlationKey: BuildCorrelationKey("write", wire.Path),
            cancellationToken).ConfigureAwait(false);

        return MapWriteResult(request.Id, result);
    }

    private async Task<AgentActionPayload> ComposeWritePayloadAsync(
        AgentWorkspaceRelativePath relativePath,
        string proposedText,
        CancellationToken cancellationToken)
    {
        var readResult = await _broker.RequestAsync(
            new AgentReadFileActionPayload(relativePath),
            correlationKey: null,
            cancellationToken).ConfigureAwait(false);

        if (readResult.ResultKind == AgentActionResultKind.Succeeded)
        {
            if (readResult.Revision == default)
            {
                throw new ArgumentException("Authoritative read did not return a base revision.");
            }

            return new AgentReplaceFileActionPayload(
                relativePath,
                readResult.Revision,
                proposedText);
        }

        if (readResult.ResultKind == AgentActionResultKind.Failed
            && readResult.FailureKind == AgentActionFailureKind.ExecutionFailed)
        {
            return new AgentCreateFileActionPayload(relativePath, proposedText);
        }

        throw new ArgumentException(BoundSummary(readResult.Summary));
    }

    private static AcpJsonRpcResponse MapReadResult(AcpJsonRpcRequestId id, AgentActionResult result)
    {
        if (result.ResultKind == AgentActionResultKind.Succeeded)
        {
            return Success(id, new AcpReadTextFileResponseWire
            {
                Content = result.Content ?? string.Empty,
            });
        }

        return MapFailure(id, result);
    }

    private static AcpJsonRpcResponse MapWriteResult(AcpJsonRpcRequestId id, AgentActionResult result)
    {
        if (result.ResultKind == AgentActionResultKind.Succeeded)
        {
            return Success(id, new AcpWriteTextFileResponseWire());
        }

        return MapFailure(id, result);
    }

    private static AcpJsonRpcResponse MapFailure(AcpJsonRpcRequestId id, AgentActionResult result) =>
        result.ResultKind switch
        {
            AgentActionResultKind.Cancelled => Error(
                id,
                AcpJsonRpcErrorCode.RequestCancelled,
                BoundSummary(result.Summary)),
            AgentActionResultKind.Revoked when result.FailureKind == AgentActionFailureKind.StaleBaseRevision =>
                Error(id, AcpJsonRpcErrorCode.InternalError, BoundSummary(result.Summary)),
            AgentActionResultKind.Conflict when result.FailureKind == AgentActionFailureKind.StaleBaseRevision =>
                Error(id, AcpJsonRpcErrorCode.InternalError, BoundSummary(result.Summary)),
            AgentActionResultKind.Denied when result.FailureKind == AgentActionFailureKind.PermissionDenied =>
                Error(id, AcpJsonRpcErrorCode.InternalError, BoundSummary(result.Summary)),
            AgentActionResultKind.Denied when result.FailureKind == AgentActionFailureKind.BrokerRevoked =>
                Error(id, AcpJsonRpcErrorCode.InternalError, BoundSummary(result.Summary)),
            AgentActionResultKind.Failed when result.FailureKind == AgentActionFailureKind.PathRejected =>
                Error(id, AcpJsonRpcErrorCode.InvalidParams, BoundSummary(result.Summary)),
            AgentActionResultKind.Failed when result.FailureKind == AgentActionFailureKind.ExecutionFailed =>
                Error(id, AcpJsonRpcErrorCode.ResourceNotFound, BoundSummary(result.Summary)),
            _ => Error(id, AcpJsonRpcErrorCode.InternalError, BoundSummary(result.Summary)),
        };

    private static string BuildCorrelationKey(string operation, string absolutePath) =>
        $"acp:{operation}:{absolutePath}";

    private static string BoundSummary(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return "ACP filesystem action was denied.";
        }

        return summary.Length <= 240 ? summary : summary[..240];
    }

    private static AcpJsonRpcResponse Success(AcpJsonRpcRequestId id, object result) =>
        new()
        {
            Id = id,
            Result = System.Text.Json.JsonSerializer.SerializeToElement(
                result,
                AcpJsonSerializerOptionsFactory.SharedOptions),
        };

    private static AcpJsonRpcResponse Error(AcpJsonRpcRequestId id, int code, string message) =>
        new()
        {
            Id = id,
            Error = new AcpJsonRpcError
            {
                Code = code,
                Message = message,
            },
        };

    private static AcpJsonRpcResponse InvalidParams(AcpJsonRpcRequestId id, string message) =>
        Error(id, AcpJsonRpcErrorCode.InvalidParams, message);
}
