using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Debugging.Application;

namespace Zaide.Services;

/// <summary>
/// Owns one NetCoreDbg child process and a Content-Length DAP transport.
/// </summary>
internal sealed class NetCoreDbgAdapterSession : IDebugAdapterSession
{
    private readonly DebugAdapterStartOptions _options;
    private readonly List<string> _stderrLines = new();
    private readonly object _stderrLock = new();
    private Process? _process;
    private DapContentLengthTransport? _transport;
    private bool _disposed;
    private int _exitSignaled;

    public NetCoreDbgAdapterSession(DebugAdapterStartOptions options)
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
    public IReadOnlyList<string> StderrLines
    {
        get
        {
            lock (_stderrLock)
                return _stderrLines.ToArray();
        }
    }

    /// <inheritdoc />
    public event Action<long>? ProcessExited;

    /// <inheritdoc />
    public event Action<DapStoppedEvent>? Stopped;

    /// <inheritdoc />
    public event Action<DapContinuedEvent>? Continued;

    /// <inheritdoc />
    public event Action<DapOutputEvent>? Output;

    /// <inheritdoc />
    public event Action<long>? Terminated;

    /// <inheritdoc />
    public event Action<DapExitedEvent>? Exited;

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        var psi = new ProcessStartInfo
        {
            FileName = _options.AdapterPath,
            Arguments = "--interpreter=vscode",
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
            throw new InvalidOperationException($"Failed to start debug adapter: {_options.AdapterPath}");

        _ = Task.Run(async () =>
        {
            try
            {
                while (_process is { HasExited: false })
                {
                    var line = await _process.StandardError.ReadLineAsync().ConfigureAwait(false);
                    if (line is null)
                        break;

                    lock (_stderrLock)
                        _stderrLines.Add(line);
                }
            }
            catch
            {
                // Process teardown races are expected.
            }
        });

        _transport = new DapContentLengthTransport(
            _process.StandardOutput.BaseStream,
            _process.StandardInput.BaseStream);

        RegisterEventHandlers(_transport);
        _transport.StartListening();
    }

    /// <inheritdoc />
    public Task<JsonElement?> InitializeAsync(CancellationToken cancellationToken) =>
        RequestWithTimeoutAsync(
            "initialize",
            new
            {
                clientID = "zaide",
                clientName = "Zaide",
                adapterID = "coreclr",
                pathFormat = "path",
                linesStartAt1 = true,
                columnsStartAt1 = true,
            },
            DebugSessionTimeouts.Initialize,
            cancellationToken);

    /// <inheritdoc />
    public Task LaunchAsync(
        string programPath,
        string workingDirectory,
        bool stopAtEntry,
        CancellationToken cancellationToken) =>
        RequestWithTimeoutAsync(
            "launch",
            new
            {
                program = programPath,
                cwd = workingDirectory,
                stopAtEntry,
                console = "internalConsole",
            },
            DebugSessionTimeouts.LaunchConfiguration,
            cancellationToken);

    /// <inheritdoc />
    public Task<JsonElement?> SetBreakpointsAsync(
        string sourcePath,
        IReadOnlyList<int> lines,
        CancellationToken cancellationToken)
    {
        var breakpoints = new object[lines.Count];
        for (var i = 0; i < lines.Count; i++)
            breakpoints[i] = new { line = lines[i] };

        return RequestWithTimeoutAsync(
            "setBreakpoints",
            new
            {
                source = new { path = sourcePath },
                breakpoints,
            },
            DebugSessionTimeouts.LaunchConfiguration,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<JsonElement?> ConfigurationDoneAsync(CancellationToken cancellationToken) =>
        RequestWithTimeoutAsync(
            "configurationDone",
            new { },
            DebugSessionTimeouts.LaunchConfiguration,
            cancellationToken);

    /// <inheritdoc />
    public Task<JsonElement?> RequestThreadsAsync(CancellationToken cancellationToken) =>
        RequestWithTimeoutAsync("threads", new { }, DebugSessionTimeouts.OrdinaryRequest, cancellationToken);

    /// <inheritdoc />
    public Task<JsonElement?> RequestStackTraceAsync(int threadId, CancellationToken cancellationToken) =>
        RequestWithTimeoutAsync(
            "stackTrace",
            new { threadId },
            DebugSessionTimeouts.OrdinaryRequest,
            cancellationToken);

    /// <inheritdoc />
    public Task<JsonElement?> RequestScopesAsync(int frameId, CancellationToken cancellationToken) =>
        RequestWithTimeoutAsync(
            "scopes",
            new { frameId },
            DebugSessionTimeouts.OrdinaryRequest,
            cancellationToken);

    /// <inheritdoc />
    public Task<JsonElement?> RequestVariablesAsync(
        int variablesReference,
        CancellationToken cancellationToken) =>
        RequestWithTimeoutAsync(
            "variables",
            new { variablesReference },
            DebugSessionTimeouts.OrdinaryRequest,
            cancellationToken);

    /// <inheritdoc />
    public Task ContinueAsync(int threadId, CancellationToken cancellationToken) =>
        RequestWithTimeoutAsync(
            "continue",
            new { threadId },
            DebugSessionTimeouts.OrdinaryRequest,
            cancellationToken);

    /// <inheritdoc />
    public Task PauseAsync(CancellationToken cancellationToken) =>
        RequestWithTimeoutAsync(
            "pause",
            new { },
            DebugSessionTimeouts.OrdinaryRequest,
            cancellationToken);

    /// <inheritdoc />
    public Task NextAsync(int threadId, CancellationToken cancellationToken) =>
        RequestWithTimeoutAsync(
            "next",
            new { threadId },
            DebugSessionTimeouts.OrdinaryRequest,
            cancellationToken);

    /// <inheritdoc />
    public Task StepInAsync(int threadId, CancellationToken cancellationToken) =>
        RequestWithTimeoutAsync(
            "stepIn",
            new { threadId },
            DebugSessionTimeouts.OrdinaryRequest,
            cancellationToken);

    /// <inheritdoc />
    public Task StepOutAsync(int threadId, CancellationToken cancellationToken) =>
        RequestWithTimeoutAsync(
            "stepOut",
            new { threadId },
            DebugSessionTimeouts.OrdinaryRequest,
            cancellationToken);

    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        if (_disposed || _transport is null)
            return;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(DebugSessionTimeouts.Disconnect);

        try
        {
            await _transport.RequestAsync(
                "disconnect",
                new { restart = false, terminateDebuggee = true },
                timeoutCts.Token).ConfigureAwait(false);
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
            await _process.WaitForExitAsync().WaitAsync(DebugSessionTimeouts.Disconnect).ConfigureAwait(false);
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

        if (_transport is not null)
        {
            try
            {
                await _transport.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Transport may already be torn down.
            }

            _transport = null;
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
    }

    private void RegisterEventHandlers(DapContentLengthTransport transport)
    {
        transport.RegisterEventHandler("stopped", body => HandleStopped(body));
        transport.RegisterEventHandler("continued", body => HandleContinued(body));
        transport.RegisterEventHandler("output", body => HandleOutput(body));
        transport.RegisterEventHandler("terminated", _ => HandleTerminated());
        transport.RegisterEventHandler("exited", body => HandleExited(body));
    }

    private async Task<JsonElement?> RequestWithTimeoutAsync(
        string command,
        object arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (_disposed || _transport is null)
            throw new InvalidOperationException("Debug adapter session is not connected.");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        return await _transport.RequestAsync(command, arguments, timeoutCts.Token).ConfigureAwait(false);
    }

    private void HandleStopped(JsonElement body)
    {
        try
        {
            string? reason = null;
            int? threadId = null;

            if (body.ValueKind == JsonValueKind.Object)
            {
                if (body.TryGetProperty("reason", out var reasonElement) &&
                    reasonElement.ValueKind == JsonValueKind.String)
                {
                    reason = reasonElement.GetString();
                }

                if (body.TryGetProperty("threadId", out var threadElement) &&
                    threadElement.ValueKind == JsonValueKind.Number &&
                    threadElement.TryGetInt32(out var parsedThreadId))
                {
                    threadId = parsedThreadId;
                }
            }

            Stopped?.Invoke(new DapStoppedEvent(Generation, reason, threadId));
        }
        catch
        {
            // Malformed notifications are ignored; the session stays alive.
        }
    }

    private void HandleContinued(JsonElement body)
    {
        try
        {
            int? threadId = null;

            if (body.ValueKind == JsonValueKind.Object &&
                body.TryGetProperty("threadId", out var threadElement) &&
                threadElement.ValueKind == JsonValueKind.Number &&
                threadElement.TryGetInt32(out var parsedThreadId))
            {
                threadId = parsedThreadId;
            }

            Continued?.Invoke(new DapContinuedEvent(Generation, threadId));
        }
        catch
        {
            // Malformed notifications are ignored; the session stays alive.
        }
    }

    private void HandleOutput(JsonElement body)
    {
        try
        {
            if (body.ValueKind != JsonValueKind.Object)
                return;

            if (!body.TryGetProperty("output", out var outputElement) ||
                outputElement.ValueKind != JsonValueKind.String)
            {
                return;
            }

            var output = outputElement.GetString() ?? string.Empty;
            string? category = null;
            if (body.TryGetProperty("category", out var categoryElement) &&
                categoryElement.ValueKind == JsonValueKind.String)
            {
                category = categoryElement.GetString();
            }

            Output?.Invoke(new DapOutputEvent(Generation, category, output));
        }
        catch
        {
            // Malformed notifications are ignored; the session stays alive.
        }
    }

    private void HandleTerminated()
    {
        try
        {
            Terminated?.Invoke(Generation);
        }
        catch
        {
            // Observers must not tear down the session.
        }
    }

    private void HandleExited(JsonElement body)
    {
        try
        {
            int? exitCode = null;
            if (body.ValueKind == JsonValueKind.Object &&
                body.TryGetProperty("exitCode", out var exitCodeElement) &&
                exitCodeElement.ValueKind == JsonValueKind.Number &&
                exitCodeElement.TryGetInt32(out var parsedExitCode))
            {
                exitCode = parsedExitCode;
            }

            Exited?.Invoke(new DapExitedEvent(Generation, exitCode));
        }
        catch
        {
            // Malformed notifications are ignored; the session stays alive.
        }
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
}
