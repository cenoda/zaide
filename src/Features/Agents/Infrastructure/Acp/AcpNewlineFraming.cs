using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Features.Agents.Infrastructure.Acp;

internal static class AcpNewlineFrameWriter
{
    public static async Task WriteFrameAsync(
        Stream output,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (payload.Length > AcpProtocolLimits.MaxFrameBytes)
        {
            throw new AcpProtocolException("ACP frame exceeds the configured byte limit.");
        }

        if (payload.Span.Contains((byte)'\n'))
        {
            throw new AcpProtocolException("ACP frame must not contain embedded newline characters.");
        }

        await output.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await output.WriteAsync("\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task WriteJsonFrameAsync(
        Stream output,
        ReadOnlyMemory<byte> jsonUtf8,
        CancellationToken cancellationToken)
    {
        _ = AcpMessageCodec.ValidateUtf8Frame(jsonUtf8.Span);
        await WriteFrameAsync(output, jsonUtf8, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class AcpNewlineFrameReader
{
    private readonly Stream _input;
    private readonly byte[] _buffer = new byte[4096];
    private readonly MemoryStream _lineBuffer = new();

    public AcpNewlineFrameReader(Stream input)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
    }

    public async Task<ReadOnlyMemory<byte>?> ReadFrameAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var read = await _input.ReadAsync(_buffer.AsMemory(0, _buffer.Length), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                if (_lineBuffer.Length == 0)
                {
                    return null;
                }

                throw new AcpProtocolException("ACP stream ended before newline frame terminator.");
            }

            var offset = 0;
            while (offset < read)
            {
                var newlineIndex = Array.IndexOf(_buffer, (byte)'\n', offset, read - offset);
                if (newlineIndex < 0)
                {
                    _lineBuffer.Write(_buffer, offset, read - offset);
                    if (_lineBuffer.Length > AcpProtocolLimits.MaxFrameBytes)
                    {
                        throw new AcpProtocolException("ACP frame exceeds the configured byte limit.");
                    }

                    break;
                }

                _lineBuffer.Write(_buffer, offset, newlineIndex - offset);
                var frame = _lineBuffer.ToArray();
                _lineBuffer.SetLength(0);

                if (frame.Length == 0)
                {
                    offset = newlineIndex + 1;
                    continue;
                }

                _ = AcpMessageCodec.ValidateUtf8Frame(frame);
                return frame;
            }
        }
    }
}
