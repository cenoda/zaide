using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Features.Agents.Infrastructure.Acp;

/// <summary>
/// Bounded stderr capture with redaction. Stderr is diagnostic-only and never protocol input.
/// </summary>
internal sealed class AcpBoundedStderrReader : IAsyncDisposable
{
    private readonly List<string> _lines = new();
    private readonly object _gate = new();
    private int _totalBytes;
    private Task? _readLoop;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public IReadOnlyList<string> CapturedLines
    {
        get
        {
            lock (_gate)
            {
                return _lines.ToArray();
            }
        }
    }

    public int TotalBytes
    {
        get
        {
            lock (_gate)
            {
                return _totalBytes;
            }
        }
    }

    public void Start(Stream stderr)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(stderr);

        if (_readLoop is not null)
        {
            throw new InvalidOperationException("ACP stderr reader is already started.");
        }

        _cts = new CancellationTokenSource();
        _readLoop = Task.Run(() => ReadLoopAsync(stderr, _cts.Token));
    }

    private async Task ReadLoopAsync(Stream stderr, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stderr, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        while (!cancellationToken.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException)
            {
                break;
            }

            if (line is null)
            {
                break;
            }

            AppendLine(line);
        }
    }

    private void AppendLine(string line)
    {
        var redacted = AcpStderrRedactor.Redact(line);
        var lineBytes = Encoding.UTF8.GetByteCount(redacted);
        if (lineBytes > AcpProcessLifecycleLimits.MaxStderrLineBytes)
        {
            redacted = redacted[..Math.Min(redacted.Length, AcpProcessLifecycleLimits.MaxStderrLineBytes)] + "…";
            lineBytes = Encoding.UTF8.GetByteCount(redacted);
        }

        lock (_gate)
        {
            if (_totalBytes >= AcpProcessLifecycleLimits.MaxStderrBytes)
            {
                return;
            }

            var remaining = AcpProcessLifecycleLimits.MaxStderrBytes - _totalBytes;
            if (lineBytes > remaining)
            {
                redacted = TruncateToByteBudget(redacted, remaining);
                lineBytes = Encoding.UTF8.GetByteCount(redacted);
            }

            _lines.Add(redacted);
            _totalBytes += lineBytes;
        }
    }

    private static string TruncateToByteBudget(string value, int maxBytes)
    {
        if (maxBytes <= 0)
        {
            return string.Empty;
        }

        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length <= maxBytes)
        {
            return value;
        }

        return Encoding.UTF8.GetString(bytes, 0, maxBytes);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            _cts.Dispose();
        }

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
    }
}
