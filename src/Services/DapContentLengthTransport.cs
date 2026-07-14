using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Services;

/// <summary>
/// Content-Length framed VS Code DAP transport over redirected stdio streams.
/// NetCoreDbg's <c>--interpreter=vscode</c> mode requires DAP envelopes, not JSON-RPC 2.0.
/// </summary>
internal sealed class DapContentLengthTransport : IAsyncDisposable
{
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly Dictionary<string, Action<JsonElement>> _eventHandlers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement?>> _pending = new();
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly object _readLoopGate = new();
    private int _nextSeq = 1;
    private Task? _readLoop;
    private CancellationTokenSource? _readLoopCts;
    private bool _disposed;

    public DapContentLengthTransport(Stream input, Stream output)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    public void RegisterEventHandler(string eventName, Action<JsonElement> handler)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _eventHandlers[eventName] = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public void StartListening()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_readLoopGate)
        {
            if (_readLoop is not null)
                return;

            _readLoopCts = new CancellationTokenSource();
            _readLoop = Task.Run(() => ReadLoopAsync(_readLoopCts.Token));
        }
    }

    public async Task<JsonElement?> RequestAsync(
        string command,
        object arguments,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var seq = Interlocked.Increment(ref _nextSeq);
        var tcs = new TaskCompletionSource<JsonElement?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[seq] = tcs;

        try
        {
            await WriteMessageAsync(
                new Dictionary<string, object?>
                {
                    ["seq"] = seq,
                    ["type"] = "request",
                    ["command"] = command,
                    ["arguments"] = arguments,
                },
                cancellationToken).ConfigureAwait(false);

            using var registration = cancellationToken.Register(() => CancelPendingRequest(seq, cancellationToken));
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            _pending.TryRemove(seq, out _);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        _readLoopCts?.Cancel();
        CancelAllPendingRequests();

        if (_readLoop is not null)
        {
            try
            {
                await _readLoop.ConfigureAwait(false);
            }
            catch
            {
                // Read loop teardown races are expected.
            }
        }

        _readLoopCts?.Dispose();
        _writeGate.Dispose();
    }

    private void CancelPendingRequest(int seq, CancellationToken cancellationToken)
    {
        if (_pending.TryRemove(seq, out var tcs))
            tcs.TrySetCanceled(cancellationToken);
    }

    private void CancelAllPendingRequests()
    {
        foreach (var key in _pending.Keys)
        {
            if (_pending.TryRemove(key, out var tcs))
                tcs.TrySetCanceled();
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = await ReadMessageAsync(cancellationToken).ConfigureAwait(false);
                if (message is null)
                    break;

                DispatchMessage(message.Value);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected on dispose.
        }
        catch (EndOfStreamException)
        {
            // Adapter closed stdout.
        }
        catch (IOException)
        {
            // Transport torn down.
        }
    }

    private void DispatchMessage(JsonElement message)
    {
        if (!message.TryGetProperty("type", out var typeElement) ||
            typeElement.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var type = typeElement.GetString();
        switch (type)
        {
            case "response":
                if (message.TryGetProperty("request_seq", out var requestSeqElement) &&
                    requestSeqElement.TryGetInt32(out var requestSeq) &&
                    _pending.TryRemove(requestSeq, out var tcs))
                {
                    if (message.TryGetProperty("success", out var successElement) &&
                        successElement.ValueKind == JsonValueKind.False)
                    {
                        var errorMessage = message.TryGetProperty("message", out var messageElement) &&
                                           messageElement.ValueKind == JsonValueKind.String
                            ? messageElement.GetString() ?? "DAP request failed."
                            : "DAP request failed.";
                        tcs.TrySetException(new InvalidOperationException(errorMessage));
                        return;
                    }

                    JsonElement? body = null;
                    if (message.TryGetProperty("body", out var bodyElement))
                        body = bodyElement.Clone();

                    tcs.TrySetResult(body);
                }
                break;

            case "event":
                if (message.TryGetProperty("event", out var eventElement) &&
                    eventElement.ValueKind == JsonValueKind.String)
                {
                    var eventName = eventElement.GetString();
                    if (eventName is not null &&
                        _eventHandlers.TryGetValue(eventName, out var handler))
                    {
                        JsonElement body = default;
                        if (message.TryGetProperty("body", out var bodyElement))
                            body = bodyElement.Clone();

                        try
                        {
                            handler(body);
                        }
                        catch
                        {
                            // Observers must not tear down the transport.
                        }
                    }
                }
                break;
        }
    }

    private async Task WriteMessageAsync(IReadOnlyDictionary<string, object?> payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _output.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            await _output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await _output.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task<JsonElement?> ReadMessageAsync(CancellationToken cancellationToken)
    {
        var header = await ReadHeaderAsync(cancellationToken).ConfigureAwait(false);
        if (header is null)
            return null;

        if (!header.TryGetValue("content-length", out var lengthValue) ||
            !int.TryParse(lengthValue, out var contentLength) ||
            contentLength < 0)
        {
            throw new InvalidDataException("DAP frame is missing a valid Content-Length header.");
        }

        var buffer = ArrayPool<byte>.Shared.Rent(contentLength);
        try
        {
            var offset = 0;
            while (offset < contentLength)
            {
                var read = await _input.ReadAsync(
                    buffer.AsMemory(offset, contentLength - offset),
                    cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    return null;

                offset += read;
            }

            using var document = JsonDocument.Parse(buffer.AsMemory(0, contentLength));
            return document.RootElement.Clone();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task<Dictionary<string, string>?> ReadHeaderAsync(CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (true)
        {
            var line = await ReadAsciiLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
                return headers.Count == 0 ? null : headers;

            if (line.Length == 0)
                return headers;

            var separator = line.IndexOf(':');
            if (separator <= 0)
                continue;

            headers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }
    }

    private async Task<string?> ReadAsciiLineAsync(CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();

        while (true)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(1);
            try
            {
                var read = await _input.ReadAsync(buffer.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    return builder.Length == 0 ? null : builder.ToString();

                var ch = (char)buffer[0];
                if (ch == '\n')
                {
                    if (builder.Length > 0 && builder[^1] == '\r')
                        builder.Length--;
                    return builder.ToString();
                }

                builder.Append(ch);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
