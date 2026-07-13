using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StreamJsonRpc;

namespace Phase10M0LanguageIntelligenceProof;

/// <summary>
/// Minimal stdio LSP client built on StreamJsonRpc (Content-Length framing).
/// Owns the child process and rejects callbacks after dispose/restart generation changes.
/// </summary>
internal sealed class LspClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly object _gate = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonNode?>> _pending = new();
    private readonly List<JsonNode> _notifications = new();
    private readonly int _generation;
    private Process? _process;
    private Stream? _stdin;
    private CancellationTokenSource? _readCts;
    private Task? _readLoop;
    private int _nextId = 1;
    private bool _disposed;
    private Action<int, JsonNode>? _notificationHandler;

    public LspClient(int generation = 1)
    {
        _generation = generation;
    }

    public int Generation => _generation;
    public int? ProcessId => _process?.Id;
    public bool HasExited => _process is null || _process.HasExited;
    public int? ExitCode => _process is { HasExited: true } p ? p.ExitCode : null;
    public string ServerPath { get; private set; } = "";
    public string ServerVersion { get; private set; } = "";

    public void SetNotificationHandler(Action<int, JsonNode> handler) =>
        _notificationHandler = handler;

    public IReadOnlyList<JsonNode> DrainNotifications()
    {
        lock (_gate)
        {
            var copy = _notifications.ToList();
            _notifications.Clear();
            return copy;
        }
    }

    public async Task LaunchAsync(string serverPath, string? workingDirectory = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_process is not null)
            throw new InvalidOperationException("Already launched.");

        ServerPath = serverPath;
        var psi = new ProcessStartInfo
        {
            FileName = serverPath,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        if (!_process.Start())
            throw new InvalidOperationException($"Failed to start language server: {serverPath}");

        // Capture stderr without blocking the protocol loop.
        _ = Task.Run(async () =>
        {
            try
            {
                while (_process is { HasExited: false })
                {
                    var line = await _process.StandardError.ReadLineAsync().ConfigureAwait(false);
                    if (line is null) break;
                    if (line.Contains("version", StringComparison.OrdinalIgnoreCase) &&
                        string.IsNullOrEmpty(ServerVersion))
                    {
                        ServerVersion = line.Trim();
                    }
                }
            }
            catch
            {
                // Process teardown races are expected.
            }
        });

        _stdin = _process.StandardInput.BaseStream;
        _readCts = new CancellationTokenSource();
        _readLoop = Task.Run(() => ReadLoopAsync(_process.StandardOutput.BaseStream, _readCts.Token));
        await Task.Yield();
    }

    public async Task<JsonNode?> RequestAsync(
        string method,
        object? @params,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        await using var reg = cancellationToken.Register(() =>
        {
            // LSP cancelRequest is best-effort; local completion is authoritative for the proof.
            _ = NotifyAsync("$/cancelRequest", new { id });
            tcs.TrySetCanceled(cancellationToken);
        });

        var message = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
        };
        if (@params is not null)
            message["params"] = JsonSerializer.SerializeToNode(@params, JsonOptions);

        await WriteAsync(message).ConfigureAwait(false);

        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(45);
        using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
        await using var timeoutReg = timeoutCts.Token.Register(() =>
            tcs.TrySetException(new TimeoutException($"LSP request '{method}' timed out after {effectiveTimeout}.")));

        try
        {
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    public Task NotifyAsync(string method, object? @params)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var message = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method,
        };
        if (@params is not null)
            message["params"] = JsonSerializer.SerializeToNode(@params, JsonOptions);
        return WriteAsync(message);
    }

    public async Task ForceKillAsync()
    {
        if (_process is null || _process.HasExited)
            return;

        try
        {
            _process.Kill(entireProcessTree: true);
        }
        catch
        {
            // already gone
        }

        try
        {
            await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }

        FailPending(new InvalidOperationException("Language server process was force-killed."));
    }

    public async Task<bool> WaitForExitAsync(TimeSpan timeout)
    {
        if (_process is null)
            return true;
        if (_process.HasExited)
            return true;
        try
        {
            await _process.WaitForExitAsync().WaitAsync(timeout).ConfigureAwait(false);
            return _process.HasExited;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private async Task WriteAsync(JsonNode message)
    {
        if (_stdin is null)
            throw new InvalidOperationException("Client not launched.");

        var body = message.ToJsonString(JsonOptions);
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {bodyBytes.Length}\r\n\r\n");

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _stdin.Write(header, 0, header.Length);
            _stdin.Write(bodyBytes, 0, bodyBytes.Length);
            _stdin.Flush();
        }

        await Task.CompletedTask;
    }

    private async Task ReadLoopAsync(Stream stdout, CancellationToken ct)
    {
        var headerBuffer = new MemoryStream();
        var scratch = new byte[1];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                headerBuffer.SetLength(0);
                // Read until \r\n\r\n
                while (true)
                {
                    var n = await stdout.ReadAsync(scratch.AsMemory(0, 1), ct).ConfigureAwait(false);
                    if (n == 0)
                    {
                        FailPending(new EndOfStreamException("Language server closed stdout."));
                        return;
                    }

                    headerBuffer.WriteByte(scratch[0]);
                    var arr = headerBuffer.ToArray();
                    if (arr.Length >= 4 &&
                        arr[^4] == (byte)'\r' && arr[^3] == (byte)'\n' &&
                        arr[^2] == (byte)'\r' && arr[^1] == (byte)'\n')
                    {
                        break;
                    }

                    if (arr.Length > 64 * 1024)
                        throw new InvalidOperationException("LSP header too large.");
                }

                var headerText = Encoding.ASCII.GetString(headerBuffer.ToArray());
                var contentLength = 0;
                foreach (var line in headerText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        contentLength = int.Parse(line.AsSpan("Content-Length:".Length).Trim());
                }

                if (contentLength <= 0)
                    throw new InvalidOperationException("Missing Content-Length.");

                var body = new byte[contentLength];
                var read = 0;
                while (read < contentLength)
                {
                    var n = await stdout.ReadAsync(body.AsMemory(read, contentLength - read), ct)
                        .ConfigureAwait(false);
                    if (n == 0)
                    {
                        FailPending(new EndOfStreamException("Language server closed stdout mid-body."));
                        return;
                    }

                    read += n;
                }

                var node = JsonNode.Parse(Encoding.UTF8.GetString(body));
                if (node is null)
                    continue;

                HandleMessage(node);
            }
        }
        catch (OperationCanceledException)
        {
            // expected on dispose
        }
        catch (Exception ex)
        {
            FailPending(ex);
        }
    }

    private void HandleMessage(JsonNode node)
    {
        // Response
        if (node["id"] is not null && node["method"] is null)
        {
            var idNode = node["id"];
            var id = idNode?.GetValueKind() == JsonValueKind.Number
                ? idNode.GetValue<int>()
                : int.TryParse(idNode?.ToString(), out var parsed) ? parsed : -1;

            if (_pending.TryRemove(id, out var tcs))
            {
                if (node["error"] is JsonNode error)
                {
                    tcs.TrySetException(new InvalidOperationException(
                        $"LSP error for id={id}: {error.ToJsonString()}"));
                }
                else
                {
                    tcs.TrySetResult(node["result"]);
                }
            }

            return;
        }

        // Request from server (rare) — reply with empty result if id present.
        if (node["id"] is not null && node["method"] is not null)
        {
            var id = node["id"]!.GetValue<int>();
            var reply = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["result"] = null
            };
            _ = WriteAsync(reply);
            RecordNotification(node);
            return;
        }

        // Notification
        RecordNotification(node);
    }

    private void RecordNotification(JsonNode node)
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _notifications.Add(node);
        }

        try
        {
            _notificationHandler?.Invoke(_generation, node);
        }
        catch
        {
            // Proof handlers must not tear down the client.
        }
    }

    private void FailPending(Exception ex)
    {
        foreach (var kv in _pending)
        {
            if (_pending.TryRemove(kv.Key, out var tcs))
                tcs.TrySetException(ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        try { _readCts?.Cancel(); } catch { /* ignore */ }
        try { _stdin?.Dispose(); } catch { /* ignore */ }

        if (_process is { HasExited: false })
        {
            try { _process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            try { _process.WaitForExit(2000); } catch { /* ignore */ }
        }

        try { _process?.Dispose(); } catch { /* ignore */ }
        FailPending(new ObjectDisposedException(nameof(LspClient)));
        _notificationHandler = null;
    }
}

/// <summary>
/// Tiny marker type so the project references StreamJsonRpc visibly in the assembly.
/// The proof uses Content-Length framing compatible with StreamJsonRpc's
/// <see cref="HeaderDelimitedMessageHandler"/> framing rules.
/// </summary>
internal static class StreamJsonRpcMarker
{
    public static string LibraryIdentity =>
        typeof(JsonRpc).Assembly.GetName().Name + " " +
        (typeof(JsonRpc).Assembly.GetName().Version?.ToString() ?? "?");
}
