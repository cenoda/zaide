using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Services;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 12 F1 unit tests for thread-safe DAP pending-request ownership.
/// </summary>
[CollectionDefinition("DapContentLengthTransportTests", DisableParallelization = true)]
public sealed class DapContentLengthTransportTestsCollection;

[Collection("DapContentLengthTransportTests")]
public sealed class DapContentLengthTransportTests
{
    [Fact]
    public async Task SingleRequest_ReceivesResponse_Completes()
    {
        await using var harness = DapTransportTestHarness.Create();
        harness.Transport.StartListening();

        var request = harness.Transport.RequestAsync("only", new { }, CancellationToken.None);
        var observed = await harness.ReadNextRequestAsync();
        await harness.WriteResponseAsync(observed.Seq, new { ok = true });

        var body = await request;
        Assert.True(body!.Value.GetProperty("ok").GetBoolean());
        Assert.Equal(0, harness.GetPendingCount());
    }

    [Fact]
    public async Task OverlappingRequests_ReceiveOutOfOrderResponses_CorrelateCorrectly()
    {
        await using var harness = DapTransportTestHarness.Create();
        harness.Transport.StartListening();

        var first = harness.Transport.RequestAsync("first", new { }, CancellationToken.None);
        var second = harness.Transport.RequestAsync("second", new { }, CancellationToken.None);
        await Task.Yield();

        var firstRequest = await harness.ReadNextRequestAsync();
        var secondRequest = await harness.ReadNextRequestAsync();

        Assert.Equal("first", firstRequest.Command);
        Assert.Equal("second", secondRequest.Command);
        Assert.NotEqual(firstRequest.Seq, secondRequest.Seq);

        await harness.WriteResponseAsync(secondRequest.Seq, new { marker = "second-body" });
        await harness.WriteResponseAsync(firstRequest.Seq, new { marker = "first-body" });

        var firstBody = await first;
        var secondBody = await second;

        Assert.Equal("first-body", firstBody!.Value.GetProperty("marker").GetString());
        Assert.Equal("second-body", secondBody!.Value.GetProperty("marker").GetString());
        Assert.Equal(0, harness.GetPendingCount());
    }

    [Fact]
    public async Task ConcurrentRequests_AllCompleteWithoutLostResponses()
    {
        const int requestCount = 32;

        await using var harness = DapTransportTestHarness.Create();
        harness.Transport.StartListening();

        var requests = Enumerable.Range(0, requestCount)
            .Select(i => harness.Transport.RequestAsync($"command-{i}", new { index = i }, CancellationToken.None))
            .ToArray();
        await Task.Yield();

        var observed = new ConcurrentDictionary<int, string>();
        for (var i = 0; i < requestCount; i++)
        {
            var request = await harness.ReadNextRequestAsync();
            observed[request.Seq] = request.Command;
            await harness.WriteResponseAsync(request.Seq, new { command = request.Command });
        }

        var responses = await Task.WhenAll(requests);
        Assert.Equal(requestCount, observed.Count);
        Assert.Equal(requestCount, responses.Length);
        Assert.All(responses, response =>
        {
            Assert.NotNull(response);
            Assert.StartsWith("command-", response!.Value.GetProperty("command").GetString());
        });
        Assert.Equal(0, harness.GetPendingCount());
    }

    [Fact]
    public async Task CancellationRacingResponseDispatch_CompletesWithoutHangOrStalePending()
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            await using var harness = DapTransportTestHarness.Create();
            harness.Transport.StartListening();

            using var cts = new CancellationTokenSource();
            var requestTask = harness.Transport.RequestAsync("race", new { attempt }, cts.Token);
            var observed = await harness.ReadNextRequestAsync();

            var release = new Barrier(2);
            var cancelTask = Task.Run(() =>
            {
                release.SignalAndWait();
                cts.Cancel();
            });
            var respondTask = Task.Run(async () =>
            {
                release.SignalAndWait();
                await harness.WriteResponseAsync(observed.Seq, new { marker = "late-response" });
            });

            await Task.WhenAll(cancelTask, respondTask);

            var completed = await Task.WhenAny(requestTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.Same(requestTask, completed);

            if (requestTask.Status == TaskStatus.Canceled)
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => requestTask);
            }
            else
            {
                Assert.Equal(TaskStatus.RanToCompletion, requestTask.Status);
                var body = await requestTask;
                Assert.Equal("late-response", body!.Value.GetProperty("marker").GetString());
            }

            Assert.Equal(0, harness.GetPendingCount());
        }
    }

    [Fact]
    public async Task DisposeRacingOutstandingRequests_CancelsAllWithoutCollectionExceptions()
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            await using var harness = DapTransportTestHarness.Create();
            harness.Transport.StartListening();

            var requests = Enumerable.Range(0, 8)
                .Select(i => harness.Transport.RequestAsync($"outstanding-{i}", new { attempt, i }, CancellationToken.None))
                .ToArray();
            await Task.Yield();

            for (var i = 0; i < requests.Length; i++)
                await harness.ReadNextRequestAsync();

            await harness.Transport.DisposeAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.WhenAll(requests));
            Assert.Equal(0, harness.GetPendingCount());
        }
    }

    private sealed class DapTransportTestHarness : IAsyncDisposable
    {
        private readonly BlockingMemoryStream _input = new();
        private readonly SynchronizedMemoryStream _output = new();
        private readonly SemaphoreSlim _outputGate = new(1, 1);
        private readonly FieldInfo _pendingField;

        private DapTransportTestHarness()
        {
            Transport = new DapContentLengthTransport(_input, _output);
            _pendingField = typeof(DapContentLengthTransport).GetField("_pending", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("DapContentLengthTransport._pending field was not found.");
        }

        public DapContentLengthTransport Transport { get; }

        public static DapTransportTestHarness Create() => new();

        public int GetPendingCount()
        {
            var pending = _pendingField.GetValue(Transport);
            return pending is ICollection collection ? collection.Count : 0;
        }

        public async Task<DapObservedRequest> ReadNextRequestAsync()
        {
            var deadline = Environment.TickCount64 + 5_000;
            while (true)
            {
                if (Environment.TickCount64 > deadline)
                    throw new TimeoutException("Timed out waiting for a DAP request frame.");

                if (_output.BufferedLength == 0)
                {
                    await Task.Delay(1).ConfigureAwait(false);
                    continue;
                }

                await _outputGate.WaitAsync().ConfigureAwait(false);
                try
                {
                    _output.ResetReadPosition();
                    try
                    {
                        var (payload, consumedBytes) = await ReadNextDapPayloadWithLengthAsync(_output, CancellationToken.None)
                            .ConfigureAwait(false);
                        _output.ConsumeBufferedBytes(consumedBytes);

                        using var document = JsonDocument.Parse(payload);
                        var root = document.RootElement;
                        return new DapObservedRequest(
                            root.GetProperty("seq").GetInt32(),
                            root.GetProperty("command").GetString() ?? string.Empty);
                    }
                    catch (InvalidDataException)
                    {
                        // Transport writes header/body as separate operations; retry until the frame is complete.
                    }
                    catch (EndOfStreamException)
                    {
                    }
                }
                finally
                {
                    _outputGate.Release();
                }

                await Task.Delay(1).ConfigureAwait(false);
            }
        }

        public async Task WriteResponseAsync(int requestSeq, object body)
        {
            await WriteDapFrameAsync(
                _input,
                new Dictionary<string, object?>
                {
                    ["seq"] = requestSeq + 1000,
                    ["type"] = "response",
                    ["request_seq"] = requestSeq,
                    ["success"] = true,
                    ["body"] = body,
                }).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            await Transport.DisposeAsync().ConfigureAwait(false);
            await _input.DisposeAsync().ConfigureAwait(false);
            await _output.DisposeAsync().ConfigureAwait(false);
            _outputGate.Dispose();
        }

        private static async Task<(string Payload, int ConsumedBytes)> ReadNextDapPayloadWithLengthAsync(
            Stream stream,
            CancellationToken cancellationToken)
        {
            var startPosition = stream.Position;
            var header = await ReadHeaderAsync(stream, cancellationToken).ConfigureAwait(false);
            if (!header.TryGetValue("content-length", out var lengthValue) ||
                !int.TryParse(lengthValue, out var contentLength) ||
                contentLength < 0)
            {
                throw new InvalidDataException("Missing Content-Length header.");
            }

            var buffer = new byte[contentLength];
            var offset = 0;
            while (offset < contentLength)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(offset, contentLength - offset), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                    throw new EndOfStreamException("Request frame was truncated.");

                offset += read;
            }

            var consumedBytes = (int)(stream.Position - startPosition);
            return (Encoding.UTF8.GetString(buffer), consumedBytes);
        }

        private static Task WriteDapFrameAsync(Stream stream, IReadOnlyDictionary<string, object?> payload)
        {
            var json = JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            var header = Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");
            var frame = new byte[header.Length + bytes.Length];
            Buffer.BlockCopy(header, 0, frame, 0, header.Length);
            Buffer.BlockCopy(bytes, 0, frame, header.Length, bytes.Length);
            return stream.WriteAsync(frame).AsTask();
        }

        private static async Task<Dictionary<string, string>> ReadHeaderAsync(Stream stream, CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            while (true)
            {
                var line = await ReadAsciiLineAsync(stream, cancellationToken).ConfigureAwait(false);
                if (line is null)
                    return headers;

                if (line.Length == 0)
                    return headers;

                var separator = line.IndexOf(':');
                if (separator <= 0)
                    continue;

                headers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }
        }

        private static async Task<string?> ReadAsciiLineAsync(Stream stream, CancellationToken cancellationToken)
        {
            var builder = new StringBuilder();

            while (true)
            {
                var buffer = new byte[1];
                var read = await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
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
        }
    }

    private sealed record DapObservedRequest(int Seq, string Command);

    private sealed class BlockingMemoryStream : Stream
    {
        private readonly MemoryStream _inner = new();
        private readonly object _sync = new();
        private int _readPosition;
        private bool _disposed;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length { get { lock (_sync) return _inner.Length; } }
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
            lock (_sync)
                _inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            while (true)
            {
                lock (_sync)
                {
                    if (_disposed)
                        return 0;

                    if (_readPosition < _inner.Length)
                    {
                        _inner.Position = _readPosition;
                        var read = _inner.Read(buffer, offset, count);
                        _readPosition = (int)_inner.Position;
                        return read;
                    }
                }

                Thread.Sleep(1);
            }
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (_sync)
                {
                    if (_disposed)
                        return 0;

                    if (_readPosition < _inner.Length)
                    {
                        _inner.Position = _readPosition;
                        var read = _inner.Read(buffer, offset, count);
                        _readPosition = (int)_inner.Position;
                        return read;
                    }
                }

                await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            }
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                if (_disposed)
                    return new ValueTask<int>(0);

                if (_readPosition < _inner.Length)
                {
                    _inner.Position = _readPosition;
                    var readTask = _inner.ReadAsync(buffer, cancellationToken);
                    return AwaitAndTrackPositionAsync(readTask);
                }
            }

            return new ValueTask<int>(WaitForDataThenReadAsync(buffer, cancellationToken));
        }

        private async ValueTask<int> AwaitAndTrackPositionAsync(ValueTask<int> readTask)
        {
            var read = await readTask.ConfigureAwait(false);
            lock (_sync)
                _readPosition = (int)_inner.Position;

            return read;
        }

        private async Task<int> WaitForDataThenReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var hasData = false;
                ValueTask<int> readTask = default;
                lock (_sync)
                {
                    if (_disposed)
                        return 0;

                    if (_readPosition < _inner.Length)
                    {
                        _inner.Position = _readPosition;
                        readTask = _inner.ReadAsync(buffer, cancellationToken);
                        hasData = true;
                    }
                }

                if (hasData)
                {
                    var read = await readTask.ConfigureAwait(false);
                    lock (_sync)
                        _readPosition = (int)_inner.Position;

                    return read;
                }

                await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (_sync)
            {
                _inner.Position = _inner.Length;
                _inner.Write(buffer, offset, count);
            }
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                _inner.Position = _inner.Length;
                return _inner.WriteAsync(buffer, offset, count, cancellationToken);
            }
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                _inner.Position = _inner.Length;
                return _inner.WriteAsync(buffer, cancellationToken);
            }
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_sync)
                {
                    _disposed = true;
                    _inner.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }

    private sealed class SynchronizedMemoryStream : Stream
    {
        private readonly MemoryStream _inner = new();
        private readonly object _sync = new();

        public int BufferedLength
        {
            get
            {
                lock (_sync)
                    return (int)_inner.Length;
            }
        }

        public void ResetReadPosition()
        {
            lock (_sync)
                _inner.Position = 0;
        }

        public void ConsumeBufferedBytes(int byteCount)
        {
            lock (_sync)
            {
                var remaining = (int)_inner.Length - byteCount;
                if (remaining <= 0)
                {
                    _inner.SetLength(0);
                    _inner.Position = 0;
                    return;
                }

                var buffer = _inner.GetBuffer();
                Buffer.BlockCopy(buffer, byteCount, buffer, 0, remaining);
                _inner.SetLength(remaining);
                _inner.Position = 0;
            }
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override long Length { get { lock (_sync) return _inner.Length; } }

        public override long Position
        {
            get { lock (_sync) return _inner.Position; }
            set { lock (_sync) _inner.Position = value; }
        }

        public override void Flush()
        {
            lock (_sync)
                _inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            lock (_sync)
                return _inner.Read(buffer, offset, count);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            lock (_sync)
                return _inner.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            lock (_sync)
                return _inner.ReadAsync(buffer, cancellationToken);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (_sync)
            {
                _inner.Position = _inner.Length;
                _inner.Write(buffer, offset, count);
            }
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                _inner.Position = _inner.Length;
                return _inner.WriteAsync(buffer, offset, count, cancellationToken);
            }
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                _inner.Position = _inner.Length;
                return _inner.WriteAsync(buffer, cancellationToken);
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            lock (_sync)
                return _inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            lock (_sync)
                _inner.SetLength(value);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_sync)
                    _inner.Dispose();
            }

            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            lock (_sync)
                _inner.Dispose();

            return base.DisposeAsync();
        }
    }
}