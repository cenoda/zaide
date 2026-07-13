using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StreamJsonRpc;

namespace Zaide.Services;

/// <summary>
/// Owns one csharp-ls child process and a Content-Length StreamJsonRpc transport.
/// </summary>
internal sealed class CsharpLsSession : ILanguageServerSession
{
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(10);

    private readonly LanguageServerStartOptions _options;
    private Process? _process;
    private JsonRpc? _rpc;
    private bool _disposed;
    private int _exitSignaled;

    public CsharpLsSession(LanguageServerStartOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public long Generation => _options.Generation;

    /// <inheritdoc />
    public int? ProcessId => _process?.HasExited == false ? _process.Id : _process?.Id;

    /// <inheritdoc />
    public bool HasExited => _process is null || _process.HasExited;

    /// <inheritdoc />
    public event Action<long>? ProcessExited;

    /// <summary>
    /// Launches the process and completes LSP initialize/initialized.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var psi = new ProcessStartInfo
        {
            FileName = _options.ServerPath,
            WorkingDirectory = _options.WorkspaceFolderPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        _process = new Process
        {
            StartInfo = psi,
            EnableRaisingEvents = true,
        };

        _process.Exited += OnProcessExited;

        if (!_process.Start())
            throw new InvalidOperationException($"Failed to start language server: {_options.ServerPath}");

        // Drain stderr on a background task so protocol reads never block.
        _ = Task.Run(async () =>
        {
            try
            {
                while (_process is { HasExited: false })
                {
                    var line = await _process.StandardError.ReadLineAsync().ConfigureAwait(false);
                    if (line is null)
                        break;
                }
            }
            catch
            {
                // Process teardown races are expected.
            }
        });

        var formatter = new SystemTextJsonFormatter
        {
            JsonSerializerOptions =
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            },
        };

        var handler = new HeaderDelimitedMessageHandler(
            _process.StandardInput.BaseStream,
            _process.StandardOutput.BaseStream,
            formatter);

        _rpc = new JsonRpc(handler);
        _rpc.StartListening();
        _rpc.Disconnected += (_, e) => SignalProcessExited();

        var workspaceUri = LanguageDocumentUri.FromPath(_options.WorkspaceFolderPath);
        var initParams = BuildInitializeParams(workspaceUri, _options);

        _ = await _rpc.InvokeWithCancellationAsync<JsonElement?>(
            "initialize",
            new object?[] { initParams },
            cancellationToken).ConfigureAwait(false);

        await _rpc.NotifyAsync("initialized", new { }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ShutdownAsync(CancellationToken cancellationToken)
    {
        if (_disposed || _rpc is null)
            return;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(ShutdownTimeout);

        try
        {
            await _rpc.InvokeWithCancellationAsync<object?>(
                "shutdown",
                Array.Empty<object?>(),
                timeoutCts.Token).ConfigureAwait(false);
            await _rpc.NotifyAsync("exit", new { }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await ForceKillAsync().ConfigureAwait(false);
            throw;
        }
        catch
        {
            await ForceKillAsync().ConfigureAwait(false);
            throw;
        }

        if (_process is not null && !_process.HasExited)
        {
            try
            {
                await _process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch
            {
                await ForceKillAsync().ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public Task NotifyDidOpenAsync(
        string documentUri,
        int version,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (_disposed || _rpc is null)
            return Task.CompletedTask;

        return _rpc.NotifyAsync(
            "textDocument/didOpen",
            new
            {
                textDocument = new
                {
                    uri = documentUri,
                    languageId = "csharp",
                    version,
                    text,
                },
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task NotifyDidChangeAsync(
        string documentUri,
        int version,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (_disposed || _rpc is null)
            return Task.CompletedTask;

        return _rpc.NotifyAsync(
            "textDocument/didChange",
            new
            {
                textDocument = new { uri = documentUri, version },
                contentChanges = new object[] { new { text } },
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task NotifyDidCloseAsync(
        string documentUri,
        CancellationToken cancellationToken = default)
    {
        if (_disposed || _rpc is null)
            return Task.CompletedTask;

        return _rpc.NotifyAsync(
            "textDocument/didClose",
            new { textDocument = new { uri = documentUri } },
            cancellationToken);
    }

    /// <inheritdoc />
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
            // Already gone.
        }

        try
        {
            await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch
        {
            // Best effort.
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_process is not null)
            _process.Exited -= OnProcessExited;

        try
        {
            _rpc?.Dispose();
        }
        catch
        {
            // Transport may already be torn down.
        }

        if (_process is { HasExited: false })
        {
            try
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(2000);
            }
            catch
            {
                // Best effort.
            }
        }

        _process?.Dispose();
        _process = null;
        _rpc = null;
    }

    private void OnProcessExited(object? sender, EventArgs e) => SignalProcessExited();

    private void SignalProcessExited()
    {
        if (Interlocked.Exchange(ref _exitSignaled, 1) != 0)
            return;

        try
        {
            ProcessExited?.Invoke(Generation);
        }
        catch
        {
            // Observers must not tear down the session.
        }
    }

    private static Dictionary<string, object?> BuildInitializeParams(
        string workspaceUri,
        LanguageServerStartOptions options)
    {
        object? initializationOptions = options.ProjectKind == ProjectKind.SolutionX
            ? new Dictionary<string, object?> { ["solution"] = options.ProjectFilePath }
            : null;

        var initParams = new Dictionary<string, object?>
        {
            ["processId"] = Environment.ProcessId,
            ["clientInfo"] = new { name = "Zaide", version = "1.0.0" },
            ["rootUri"] = workspaceUri,
            ["rootPath"] = options.WorkspaceFolderPath,
            ["capabilities"] = new Dictionary<string, object?>
            {
                ["general"] = new
                {
                    positionEncodings = new[] { "utf-16", "utf-8" },
                },
                ["workspace"] = new Dictionary<string, object?>
                {
                    ["workspaceFolders"] = true,
                },
            },
            ["workspaceFolders"] = new object[]
            {
                new
                {
                    uri = workspaceUri,
                    name = Path.GetFileName(options.WorkspaceFolderPath.TrimEnd(Path.DirectorySeparatorChar)),
                },
            },
            ["trace"] = "off",
        };

        if (initializationOptions is not null)
            initParams["initializationOptions"] = initializationOptions;

        return initParams;
    }

}
