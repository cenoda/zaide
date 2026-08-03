using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts;

namespace Zaide.Features.Agents.Infrastructure.Acp;

/// <summary>
/// Owns one ACP child process, bounded stdio transport, and protocol session lifecycle.
/// </summary>
internal sealed class AcpStdioProcessHost : IAsyncDisposable
{
    private readonly IAcpChildProcess _process;
    private readonly AcpProtocolSession _session;
    private readonly AcpBoundedStderrReader _stderrReader = new();
    private readonly CancellationTokenSource _processExitCts = new();
    private readonly object _stateGate = new();
    private AcpProcessLifecycleState _state = AcpProcessLifecycleState.Starting;
    private bool _disposed;

    private AcpStdioProcessHost(IAcpChildProcess process, AcpProtocolSession session)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _process.Exited += OnProcessExited;
        // Process may exit before EnableRaisingEvents delivers Exited (or already
        // exited before the handler was attached). Observe that immediately.
        if (_process.HasExited)
        {
            OnProcessExited(_process, EventArgs.Empty);
        }
    }

    public int? ProcessId => _process.ProcessId;

    public bool HasExited => _process.HasExited;

    public int? ExitCode => _process.ExitCode;

    public AcpNegotiatedCapabilities? NegotiatedCapabilities => _session.NegotiatedCapabilities;

    public string? ActiveSessionId => _session.ActiveSessionId;

    public IReadOnlyList<string> CapturedStderrLines => _stderrReader.CapturedLines;

    public int LateResponseCount => _session.Connection.LateResponseCount;

    public AcpProcessLifecycleState State
    {
        get
        {
            lock (_stateGate)
            {
                return _state;
            }
        }
    }

    public static async Task<AcpStdioProcessHost> StartAsync(
        AcpProcessLaunchOptions options,
        IAcpProcessLauncher launcher,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(launcher);

        var process = await launcher.StartAsync(options, cancellationToken).ConfigureAwait(false);
        var session = new AcpProtocolSession(process.StandardOutput, process.StandardInput);
        var host = new AcpStdioProcessHost(process, session);
        AcpProcessHostShutdownRegistry.Register(host);

        try
        {
            host._stderrReader.Start(process.StandardError);
            session.Start();
            host.SetState(AcpProcessLifecycleState.Running);
            return host;
        }
        catch
        {
            await host.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public Task<AcpNegotiatedCapabilities> InitializeAsync(CancellationToken cancellationToken) =>
        ExecuteWithTimeoutAsync(
            ct => _session.InitializeAsync(ct),
            AcpProcessLifecycleLimits.InitializeTimeout,
            cancellationToken);

    public Task<string> CreateSessionAsync(string absoluteWorkingDirectory, CancellationToken cancellationToken) =>
        ExecuteWithTimeoutAsync(
            ct => _session.CreateSessionAsync(absoluteWorkingDirectory, ct),
            AcpProcessLifecycleLimits.SessionOperationTimeout,
            cancellationToken);

    public Task<AcpPromptTurnResult> PromptAsync(
        string sessionId,
        IReadOnlyList<AcpContentBlock> prompt,
        CancellationToken cancellationToken) =>
        ExecuteWithTimeoutAsync(
            ct => _session.PromptAsync(sessionId, prompt, cancellationToken),
            AcpProcessLifecycleLimits.SessionOperationTimeout,
            cancellationToken);

    public Task CancelPromptAsync(string sessionId, CancellationToken cancellationToken) =>
        ExecuteWithTimeoutAsync(
            ct => _session.CancelPromptAsync(sessionId, ct),
            AcpProcessLifecycleLimits.SessionOperationTimeout,
            cancellationToken);

    public void ConfigureActionBridge(
        AcpInboundClientRequestHandler? inboundHandler,
        AcpClientCapabilities advertisedCapabilities) =>
        _session.ConfigureActionBridge(inboundHandler, advertisedCapabilities);

    public Task CancelRequestAsync(AcpJsonRpcRequestId requestId, CancellationToken cancellationToken) =>
        ExecuteWithTimeoutAsync(
            ct => _session.CancelRequestAsync(requestId, ct),
            AcpProcessLifecycleLimits.SessionOperationTimeout,
            cancellationToken);

    public Task AuthenticateAsync(string methodId, CancellationToken cancellationToken) =>
        ExecuteWithTimeoutAsync(
            ct => _session.AuthenticateAsync(methodId, ct),
            AcpProcessLifecycleLimits.InitializeTimeout,
            cancellationToken);

    public Task LogoutAsync(CancellationToken cancellationToken) =>
        ExecuteWithTimeoutAsync(
            ct => _session.LogoutAsync(ct),
            AcpProcessLifecycleLimits.InitializeTimeout,
            cancellationToken);

    private Task ExecuteWithTimeoutAsync(
        Func<CancellationToken, Task> operation,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        ExecuteWithTimeoutAsync<object?>(
            async ct =>
            {
                await operation(ct).ConfigureAwait(false);
                return null;
            },
            timeout,
            cancellationToken);

    private async Task<T> ExecuteWithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        EnsureRunning();

        using var timeoutCts = new CancellationTokenSource();
        timeoutCts.CancelAfter(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCts.Token,
            _processExitCts.Token);

        try
        {
            return await operation(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw CreateLifecycleException(
                AcpProcessLifecycleFailureKind.Cancellation,
                "ACP operation was cancelled.");
        }
        catch (OperationCanceledException) when (IsProcessExitObserved())
        {
            // Prefer process-exit over timeout/other linked cancellation so an
            // exited child fails closed immediately instead of waiting the full
            // operation budget (e.g. InitializeTimeout).
            throw CreateLifecycleException(
                AcpProcessLifecycleFailureKind.ProcessExit,
                "ACP child process exited before the operation completed.");
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw CreateLifecycleException(
                AcpProcessLifecycleFailureKind.Timeout,
                "ACP operation timed out.");
        }
        catch (OperationCanceledException)
        {
            throw CreateLifecycleException(
                AcpProcessLifecycleFailureKind.Timeout,
                "ACP operation timed out.");
        }
        catch (AcpProtocolException ex)
        {
            throw CreateLifecycleException(
                AcpProcessLifecycleFailureKind.ProtocolFailure,
                "ACP protocol operation failed.",
                ex);
        }
    }

    private bool IsProcessExitObserved()
    {
        if (_process.HasExited || _processExitCts.IsCancellationRequested)
        {
            return true;
        }

        lock (_stateGate)
        {
            return _state is AcpProcessLifecycleState.ProcessExited;
        }
    }

    private void EnsureRunning()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_stateGate)
        {
            if (_state is AcpProcessLifecycleState.ProcessExited or AcpProcessLifecycleState.Disposed)
            {
                throw CreateLifecycleException(
                    AcpProcessLifecycleFailureKind.ProcessExit,
                    "ACP child process is no longer running.");
            }
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        SetState(AcpProcessLifecycleState.ProcessExited);
        try
        {
            _processExitCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Dispose already tore down the exit signal; ignore.
        }
    }

    private void SetState(AcpProcessLifecycleState state)
    {
        lock (_stateGate)
        {
            // Terminal states must not regress: a child that exits during
            // StartAsync can race the Running transition, and Dispose always
            // ends in Disposed.
            if (_state is AcpProcessLifecycleState.Disposed)
            {
                return;
            }

            if (_state is AcpProcessLifecycleState.ProcessExited
                && state is not AcpProcessLifecycleState.Disposed)
            {
                return;
            }

            _state = state;
        }
    }

    private static AcpProcessLifecycleException CreateLifecycleException(
        AcpProcessLifecycleFailureKind kind,
        string message) =>
        new(kind, message);

    private static AcpProcessLifecycleException CreateLifecycleException(
        AcpProcessLifecycleFailureKind kind,
        string message,
        Exception innerException) =>
        new(kind, message, innerException);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        AcpProcessHostShutdownRegistry.Unregister(this);
        SetState(AcpProcessLifecycleState.Disposed);

        _process.Exited -= OnProcessExited;
        try
        {
            _processExitCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        await _stderrReader.DisposeAsync().ConfigureAwait(false);
        await _session.DisposeAsync().ConfigureAwait(false);
        await _process.DisposeAsync().ConfigureAwait(false);
        _processExitCts.Dispose();
    }
}
