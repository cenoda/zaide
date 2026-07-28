using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Features.Agents.Infrastructure.Acp;

internal delegate Task<AcpJsonRpcResponse> AcpInboundClientRequestHandler(
    AcpJsonRpcRequest request,
    CancellationToken cancellationToken);

/// <summary>
/// Bidirectional newline-delimited JSON-RPC transport over paired streams.
/// Does not own or launch a child process.
/// </summary>
internal sealed class AcpProtocolConnection : IAsyncDisposable
{
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly AcpNewlineFrameReader _reader;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<AcpJsonRpcResponse>> _pending = new();
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly object _readLoopGate = new();
    private AcpInboundClientRequestHandler? _inboundRequestHandler;
    private Action<AcpJsonRpcNotification>? _notificationHandler;
    private Task? _readLoop;
    private int _nextRequestNumber = 1;
    private bool _disposed;

    public AcpProtocolConnection(Stream input, Stream output)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _reader = new AcpNewlineFrameReader(input);
    }

    public void SetInboundRequestHandler(AcpInboundClientRequestHandler handler) =>
        _inboundRequestHandler = handler ?? throw new ArgumentNullException(nameof(handler));

    public void SetNotificationHandler(Action<AcpJsonRpcNotification> handler) =>
        _notificationHandler = handler ?? throw new ArgumentNullException(nameof(handler));

    public void StartReading()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_readLoopGate)
        {
            if (_readLoop is not null)
            {
                return;
            }

            _readLoop = Task.Run(() => ReadLoopAsync(_disposeCts.Token));
        }
    }

    public async Task<AcpJsonRpcResponse> SendRequestAsync(
        string method,
        object? parameters,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var requestId = AcpJsonRpcRequestId.FromNumber(Interlocked.Increment(ref _nextRequestNumber));
        var request = new AcpJsonRpcRequest
        {
            Id = requestId,
            Method = method,
            Params = parameters is null
                ? null
                : JsonSerializer.SerializeToElement(parameters, AcpJsonSerializerOptionsFactory.SharedOptions),
        };

        var tcs = new TaskCompletionSource<AcpJsonRpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(requestId.ToString(), tcs))
        {
            throw new AcpProtocolException("Duplicate ACP request id.");
        }

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
            await WriteRequestAsync(request, linked.Token).ConfigureAwait(false);
            using var registration = linked.Token.Register(() =>
                tcs.TrySetCanceled(linked.Token));

            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            _pending.TryRemove(requestId.ToString(), out _);
        }
    }

    public async Task SendNotificationAsync(
        string method,
        object? parameters,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var notification = new AcpJsonRpcNotification
        {
            Method = method,
            Params = parameters is null
                ? null
                : JsonSerializer.SerializeToElement(parameters, AcpJsonSerializerOptionsFactory.SharedOptions),
        };

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
        await WriteNotificationAsync(notification, linked.Token).ConfigureAwait(false);
    }

    public async Task CancelRequestAsync(AcpJsonRpcRequestId requestId, CancellationToken cancellationToken)
    {
        await SendNotificationAsync(
            AcpMethodNames.CancelRequest,
            new AcpCancelRequestParams { RequestId = requestId },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteRequestAsync(AcpJsonRpcRequest request, CancellationToken cancellationToken)
    {
        var payload = AcpMessageCodec.SerializeRequest(request);
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await AcpNewlineFrameWriter.WriteJsonFrameAsync(_output, payload, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task WriteNotificationAsync(AcpJsonRpcNotification notification, CancellationToken cancellationToken)
    {
        var payload = AcpMessageCodec.SerializeNotification(notification);
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await AcpNewlineFrameWriter.WriteJsonFrameAsync(_output, payload, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task WriteResponseAsync(AcpJsonRpcResponse response, CancellationToken cancellationToken)
    {
        var payload = AcpMessageCodec.SerializeResponse(response);
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await AcpNewlineFrameWriter.WriteJsonFrameAsync(_output, payload, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            ReadOnlyMemory<byte>? frame;
            try
            {
                frame = await _reader.ReadFrameAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (frame is null)
            {
                break;
            }

            try
            {
                await DispatchFrameAsync(frame.Value, cancellationToken).ConfigureAwait(false);
            }
            catch (AcpProtocolException)
            {
                // Fail closed on malformed frames without tearing down unrelated pending work.
            }
        }
    }

    private async Task DispatchFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
    {
        switch (AcpMessageCodec.ClassifyMessage(frame.Span))
        {
            case AcpJsonRpcMessageKind.Response:
            {
                var response = AcpMessageCodec.DeserializeResponse(frame.Span);
                if (_pending.TryRemove(response.Id.ToString(), out var tcs))
                {
                    tcs.TrySetResult(response);
                }

                break;
            }
            case AcpJsonRpcMessageKind.Notification:
            {
                var notification = AcpMessageCodec.DeserializeNotification(frame.Span);
                _notificationHandler?.Invoke(notification);
                break;
            }
            case AcpJsonRpcMessageKind.Request:
            {
                var request = AcpMessageCodec.DeserializeRequest(frame.Span);
                if (_inboundRequestHandler is null)
                {
                    await WriteResponseAsync(
                        new AcpJsonRpcResponse
                        {
                            Id = request.Id,
                            Error = new AcpJsonRpcError
                            {
                                Code = AcpJsonRpcErrorCode.InternalError,
                                Message = "ACP inbound request handler is not configured.",
                            },
                        },
                        cancellationToken).ConfigureAwait(false);
                    break;
                }

                var response = await _inboundRequestHandler(request, cancellationToken).ConfigureAwait(false);
                await WriteResponseAsync(response, cancellationToken).ConfigureAwait(false);
                break;
            }
            default:
                throw new AcpProtocolException("ACP frame is not a valid JSON-RPC 2.0 message.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _disposeCts.CancelAsync().ConfigureAwait(false);
        if (_readLoop is not null)
        {
            try
            {
                await _readLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _disposeCts.Dispose();
        _writeGate.Dispose();
    }
}
