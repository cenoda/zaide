using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Infrastructure.Acp;

/// <summary>
/// Pure ACP v1 client session over stdio streams without process ownership.
/// </summary>
internal sealed class AcpProtocolSession : IAsyncDisposable
{
    private readonly AcpProtocolConnection _connection;
    private AcpInboundClientRequestRouter _inboundRouter;
    private AcpClientCapabilities _advertisedCapabilities;
    private AcpNegotiatedCapabilities? _negotiated;
    private string? _activeSessionId;
    private bool _disposed;

    public AcpProtocolSession(Stream agentOutput, Stream agentInput)
        : this(new AcpProtocolConnection(agentOutput, agentInput))
    {
    }

    internal AcpProtocolSession(AcpProtocolConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _advertisedCapabilities = AcpClientCapabilityAdvertisement.CreateM1Profile();
        _inboundRouter = new AcpInboundClientRequestRouter(_advertisedCapabilities);
        _connection.SetInboundRequestHandler(_inboundRouter.HandleAsync);
    }

    public AcpNegotiatedCapabilities? NegotiatedCapabilities => _negotiated;

    public string? ActiveSessionId => _activeSessionId;

    internal AcpProtocolConnection Connection => _connection;

    public void Start() => _connection.StartReading();

    public void ConfigureActionBridge(
        AcpInboundClientRequestHandler? inboundHandler,
        AcpClientCapabilities advertisedCapabilities)
    {
        _advertisedCapabilities = advertisedCapabilities
            ?? throw new ArgumentNullException(nameof(advertisedCapabilities));
        _inboundRouter = new AcpInboundClientRequestRouter(_advertisedCapabilities);
        _connection.SetInboundRequestHandler(
            inboundHandler ?? _inboundRouter.HandleAsync);
    }

    public async Task<AcpNegotiatedCapabilities> InitializeAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var response = await _connection.SendRequestAsync(
            AcpMethodNames.Initialize,
            AcpClientCapabilityAdvertisement.CreateInitializeParams(
                AcpSchemaProfile.WireProtocolVersion,
                _advertisedCapabilities),
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccess)
        {
            throw CreateProtocolFailure("initialize", response.Error);
        }

        var result = AcpMessageCodec.DeserializeResult<AcpInitializeResult>(response.Result);
        if (result.ProtocolVersion != AcpSchemaProfile.WireProtocolVersion)
        {
            throw new AcpProtocolException(
                $"ACP agent negotiated unsupported protocol version {result.ProtocolVersion}.");
        }

        _negotiated = new AcpNegotiatedCapabilities(
            result.ProtocolVersion,
            result.AgentCapabilities,
            result.AuthMethods,
            result.AgentInfo);

        return _negotiated;
    }

    public async Task<string> CreateSessionAsync(string absoluteWorkingDirectory, CancellationToken cancellationToken)
    {
        EnsureInitialized();

        AcpSessionValidation.RequireAbsoluteWorkingDirectory(absoluteWorkingDirectory);

        var response = await _connection.SendRequestAsync(
            AcpMethodNames.SessionNew,
            new AcpNewSessionParams
            {
                Cwd = absoluteWorkingDirectory,
                McpServers = Array.Empty<System.Text.Json.JsonElement>(),
            },
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccess)
        {
            throw CreateProtocolFailure("session/new", response.Error);
        }

        var result = AcpMessageCodec.DeserializeResult<AcpNewSessionResult>(response.Result);
        if (string.IsNullOrWhiteSpace(result.SessionId))
        {
            throw new AcpProtocolException("ACP session/new returned an empty session id.");
        }

        _activeSessionId = result.SessionId;
        return result.SessionId;
    }

    public async Task<AcpPromptTurnResult> PromptAsync(
        string sessionId,
        IReadOnlyList<AcpContentBlock> prompt,
        CancellationToken cancellationToken)
    {
        EnsureInitialized();

        if (prompt.Count > AcpProtocolLimits.MaxPromptBlocks)
        {
            throw new AcpProtocolException("ACP prompt block count exceeded the configured limit.");
        }

        var accumulator = new AcpPromptTurnAccumulator();
        void OnNotification(AcpJsonRpcNotification notification)
        {
            if (notification.Method != AcpMethodNames.SessionUpdate)
            {
                return;
            }

            var update = AcpMessageCodec.DeserializeParams<AcpSessionUpdateNotification>(notification.Params);
            if (!string.Equals(update.SessionId, sessionId, StringComparison.Ordinal))
            {
                return;
            }

            accumulator.Add(update);
        }

        _connection.SetNotificationHandler(OnNotification);

        var response = await _connection.SendRequestAsync(
            AcpMethodNames.SessionPrompt,
            new AcpPromptParams
            {
                SessionId = sessionId,
                Prompt = prompt,
            },
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccess)
        {
            if (response.Error?.Code == AcpJsonRpcErrorCode.RequestCancelled)
            {
                return AcpPromptTurnResult.Cancelled(accumulator.Updates, accumulator.AgentMessageText);
            }

            throw CreateProtocolFailure("session/prompt", response.Error);
        }

        var result = AcpMessageCodec.DeserializeResult<AcpPromptResult>(response.Result);
        if (!AcpStopReasonWire.TryParse(result.StopReason, out var stopReason))
        {
            throw new AcpProtocolException($"ACP prompt returned unknown stop reason '{result.StopReason}'.");
        }

        return new AcpPromptTurnResult(stopReason, accumulator.Updates, accumulator.AgentMessageText);
    }

    public async Task CancelPromptAsync(string sessionId, CancellationToken cancellationToken)
    {
        EnsureInitialized();

        await _connection.SendNotificationAsync(
            AcpMethodNames.SessionCancel,
            new AcpSessionCancelParams { SessionId = sessionId },
            cancellationToken).ConfigureAwait(false);
    }

    public Task CancelRequestAsync(AcpJsonRpcRequestId requestId, CancellationToken cancellationToken) =>
        _connection.CancelRequestAsync(requestId, cancellationToken);

    private void EnsureInitialized()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_negotiated is null)
        {
            throw new AcpProtocolException("ACP initialize must complete before session methods.");
        }
    }

    private static AcpProtocolException CreateProtocolFailure(string method, AcpJsonRpcError? error)
    {
        if (error is null)
        {
            return new AcpProtocolException($"ACP {method} failed without an error payload.");
        }

        return new AcpProtocolException(error.Code, $"ACP {method} failed: {error.Message}");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _connection.DisposeAsync().ConfigureAwait(false);
    }
}

internal sealed class AcpPromptTurnResult
{
    public AcpPromptTurnResult(
        AcpStopReason stopReason,
        IReadOnlyList<AcpSessionUpdate> updates,
        string agentMessageText)
    {
        StopReason = stopReason;
        Updates = updates ?? throw new ArgumentNullException(nameof(updates));
        AgentMessageText = agentMessageText ?? throw new ArgumentNullException(nameof(agentMessageText));
    }

    public AcpStopReason StopReason { get; }

    public IReadOnlyList<AcpSessionUpdate> Updates { get; }

    public string AgentMessageText { get; }

    public static AcpPromptTurnResult Cancelled(
        IReadOnlyList<AcpSessionUpdate> updates,
        string agentMessageText) =>
        new(AcpStopReason.Cancelled, updates, agentMessageText);
}
