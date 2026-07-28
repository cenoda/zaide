using System;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Infrastructure.Acp;

namespace Zaide.Features.Agents.Application.Acp;

/// <summary>
/// Supplies ACP permission option choices without invoking Phase 17 broker authorization.
/// </summary>
internal interface IAcpPermissionChoiceSource
{
    ValueTask<AcpRequestPermissionResponseWire> ChooseAsync(
        AcpRequestPermissionRequestWire request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Fail-closed ACP permission choice source for tests and deterministic mediation.
/// </summary>
internal sealed class AcpFailClosedPermissionChoiceSource : IAcpPermissionChoiceSource
{
    public ValueTask<AcpRequestPermissionResponseWire> ChooseAsync(
        AcpRequestPermissionRequestWire request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Options.Count == 0)
        {
            return ValueTask.FromResult(new AcpRequestPermissionResponseWire
            {
                Outcome = new AcpRequestPermissionOutcomeWire
                {
                    Outcome = "cancelled",
                },
            });
        }

        var reject = request.Options[0];
        foreach (var option in request.Options)
        {
            if (string.Equals(option.Kind, "reject_once", StringComparison.Ordinal))
            {
                reject = option;
                break;
            }
        }

        return ValueTask.FromResult(new AcpRequestPermissionResponseWire
        {
            Outcome = new AcpRequestPermissionOutcomeWire
            {
                Outcome = "selected",
                OptionId = reject.OptionId,
            },
        });
    }
}

/// <summary>
/// Handles ACP session/request_permission separately from Zaide broker authorization.
/// </summary>
internal sealed class AcpClientPermissionBridge
{
    private readonly IAcpPermissionChoiceSource _choiceSource;
    private readonly string _expectedSessionId;

    public AcpClientPermissionBridge(
        string expectedSessionId,
        IAcpPermissionChoiceSource? choiceSource = null)
    {
        if (string.IsNullOrWhiteSpace(expectedSessionId))
        {
            throw new ArgumentException("ACP session id is required.", nameof(expectedSessionId));
        }

        _expectedSessionId = expectedSessionId;
        _choiceSource = choiceSource ?? new AcpFailClosedPermissionChoiceSource();
    }

    public async Task<AcpJsonRpcResponse> HandleAsync(
        AcpJsonRpcRequest request,
        CancellationToken cancellationToken)
    {
        AcpRequestPermissionRequestWire wire;
        try
        {
            wire = AcpMessageCodec.DeserializeParams<AcpRequestPermissionRequestWire>(request.Params);
        }
        catch (AcpProtocolException ex)
        {
            return InvalidParams(request.Id, ex.Message);
        }

        if (!string.Equals(wire.SessionId, _expectedSessionId, StringComparison.Ordinal))
        {
            return InvalidParams(request.Id, "ACP permission request session id mismatch.");
        }

        if (wire.ToolCall is null || string.IsNullOrWhiteSpace(wire.ToolCall.ToolCallId))
        {
            return InvalidParams(request.Id, "ACP permission request tool call is required.");
        }

        if (wire.Options.Count == 0)
        {
            return InvalidParams(request.Id, "ACP permission request options are required.");
        }

        var response = await _choiceSource.ChooseAsync(wire, cancellationToken).ConfigureAwait(false);
        return Success(request.Id, response);
    }

    private static AcpJsonRpcResponse Success(AcpJsonRpcRequestId id, object result) =>
        new()
        {
            Id = id,
            Result = System.Text.Json.JsonSerializer.SerializeToElement(
                result,
                AcpJsonSerializerOptionsFactory.SharedOptions),
        };

    private static AcpJsonRpcResponse InvalidParams(AcpJsonRpcRequestId id, string message) =>
        new()
        {
            Id = id,
            Error = new AcpJsonRpcError
            {
                Code = AcpJsonRpcErrorCode.InvalidParams,
                Message = message,
            },
        };
}
