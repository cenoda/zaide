using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Debugging.Infrastructure.Dap;

namespace Zaide.Tests.Features.Debugging.Infrastructure.Dap;

/// <summary>
/// Controllable fake <see cref="IDebugAdapterSession"/> for Phase 12 M1 lifecycle tests.
/// </summary>
internal sealed class TestDebugAdapterSession : IDebugAdapterSession
{
    private readonly List<string> _stderrLines = new();

    public required long Generation { get; init; }
    public int? ProcessId { get; init; } = 9001;
    public bool HasExited { get; private set; }
    public bool Disposed { get; private set; }
    private int _emitDisabled;
    public bool DisconnectCalled { get; private set; }
    public bool ForceKillCalled { get; private set; }
    public List<string> CallOrder { get; } = new();
    public bool EmitStoppedAfterConfigurationDone { get; set; } = true;
    public string? StoppedReason { get; set; } = "entry";
    public int? StoppedThreadId { get; set; } = 1;
    public Exception? InitializeException { get; set; }
    public Exception? LaunchException { get; set; }
    public Exception? SetBreakpointsException { get; set; }
    public Exception? ContinueException { get; set; }
    public Exception? StepException { get; set; }
    public TimeSpan? InitializeDelay { get; set; }
    public TimeSpan? ConfigurationDoneDelay { get; set; }
    public TimeSpan? ContinueDelay { get; set; }
    public TimeSpan? OrdinaryRequestDelay { get; set; }

    /// <summary>
    /// When true, continued/stopped execution events are raised asynchronously after
    /// the DAP response completes, matching production adapter timing.
    /// </summary>
    public bool DeferExecutionEvents { get; set; } = true;

    public TimeSpan ExecutionEventDelay { get; set; } = TimeSpan.FromMilliseconds(20);

    /// <summary>
    /// Optional DAP <c>setBreakpoints</c> body JSON (without outer envelope), e.g.
    /// <c>{"breakpoints":[{"line":1,"verified":true}]}</c>.
    /// </summary>
    public string? SetBreakpointsBodyJson { get; set; }

    public IReadOnlyList<string> StderrLines => _stderrLines;

    public event Action<long>? ProcessExited;
    public event Action<DapStoppedEvent>? Stopped;
    public event Action<DapContinuedEvent>? Continued;
    public event Action<DapOutputEvent>? Output;
    public event Action<long>? Terminated;
    public event Action<DapExitedEvent>? Exited;

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        CallOrder.Add("connect");
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public async Task<JsonElement?> InitializeAsync(CancellationToken cancellationToken)
    {
        CallOrder.Add("initialize");
        cancellationToken.ThrowIfCancellationRequested();
        if (InitializeDelay is not null)
            await Task.Delay(InitializeDelay.Value, cancellationToken).ConfigureAwait(false);
        if (InitializeException is not null)
            throw InitializeException;
        return default;
    }

    public Task LaunchAsync(
        string programPath,
        string workingDirectory,
        bool stopAtEntry,
        CancellationToken cancellationToken)
    {
        CallOrder.Add("launch");
        cancellationToken.ThrowIfCancellationRequested();
        if (LaunchException is not null)
            throw LaunchException;
        return Task.CompletedTask;
    }

    public Task<JsonElement?> SetBreakpointsAsync(
        string sourcePath,
        IReadOnlyList<int> lines,
        CancellationToken cancellationToken)
    {
        CallOrder.Add($"setBreakpoints:{sourcePath}:{string.Join(',', lines)}");
        cancellationToken.ThrowIfCancellationRequested();
        if (SetBreakpointsException is not null)
            throw SetBreakpointsException;

        if (SetBreakpointsBodyJson is null)
            return Task.FromResult<JsonElement?>(default);

        return Task.FromResult<JsonElement?>(JsonDocument.Parse(SetBreakpointsBodyJson).RootElement.Clone());
    }

    public async Task<JsonElement?> ConfigurationDoneAsync(CancellationToken cancellationToken)
    {
        CallOrder.Add("configurationDone");
        cancellationToken.ThrowIfCancellationRequested();

        if (ConfigurationDoneDelay is not null)
            await Task.Delay(ConfigurationDoneDelay.Value, cancellationToken).ConfigureAwait(false);

        if (EmitStoppedAfterConfigurationDone)
        {
            Stopped?.Invoke(new DapStoppedEvent(Generation, StoppedReason, StoppedThreadId));
        }

        return default;
    }

    public Task<JsonElement?> RequestThreadsAsync(CancellationToken cancellationToken)
    {
        CallOrder.Add("threads");
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<JsonElement?>(JsonDocument.Parse("{\"threads\":[{\"id\":1,\"name\":\"main\"}]}").RootElement);
    }

    public Task<JsonElement?> RequestStackTraceAsync(int threadId, CancellationToken cancellationToken)
    {
        CallOrder.Add($"stackTrace:{threadId}");
        cancellationToken.ThrowIfCancellationRequested();
        var sourcePath = StackSourcePath ?? string.Empty;
        var json =
            $"{{\"stackFrames\":[{{\"id\":10,\"name\":\"Main\",\"source\":{{\"path\":\"{sourcePath}\"}},\"line\":{StackLine}}}]}}";
        return Task.FromResult<JsonElement?>(JsonDocument.Parse(json).RootElement);
    }

    public Task<JsonElement?> RequestScopesAsync(int frameId, CancellationToken cancellationToken)
    {
        CallOrder.Add($"scopes:{frameId}");
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<JsonElement?>(JsonDocument.Parse("{\"scopes\":[{\"name\":\"Locals\",\"variablesReference\":1}]}").RootElement);
    }

    public string? StackSourcePath { get; set; } = "/tmp/Program.cs";

    public int StackLine { get; set; } = 1;

    public string VariablesJson { get; set; } =
        "{\"variables\":[{\"name\":\"count\",\"value\":\"1\",\"type\":\"int\"}]}";

    public Task<JsonElement?> RequestVariablesAsync(
        int variablesReference,
        CancellationToken cancellationToken)
    {
        CallOrder.Add($"variables:{variablesReference}");
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<JsonElement?>(JsonDocument.Parse(VariablesJson).RootElement);
    }

    public async Task ContinueAsync(int threadId, CancellationToken cancellationToken)
    {
        CallOrder.Add($"continue:{threadId}");
        cancellationToken.ThrowIfCancellationRequested();
        if (ContinueDelay is not null)
            await Task.Delay(ContinueDelay.Value, cancellationToken).ConfigureAwait(false);
        if (ContinueException is not null)
            throw ContinueException;
        EmitContinued(threadId);
    }

    public Task PauseAsync(CancellationToken cancellationToken)
    {
        CallOrder.Add("pause");
        cancellationToken.ThrowIfCancellationRequested();
        Stopped?.Invoke(new DapStoppedEvent(Generation, "pause", StoppedThreadId));
        return Task.CompletedTask;
    }

    public Task NextAsync(int threadId, CancellationToken cancellationToken)
    {
        CallOrder.Add($"next:{threadId}");
        cancellationToken.ThrowIfCancellationRequested();
        if (StepException is not null)
            throw StepException;
        EmitStepStopped(threadId);
        return Task.CompletedTask;
    }

    public Task StepInAsync(int threadId, CancellationToken cancellationToken)
    {
        CallOrder.Add($"stepIn:{threadId}");
        cancellationToken.ThrowIfCancellationRequested();
        EmitStepStopped(threadId);
        return Task.CompletedTask;
    }

    public Task StepOutAsync(int threadId, CancellationToken cancellationToken)
    {
        CallOrder.Add($"stepOut:{threadId}");
        cancellationToken.ThrowIfCancellationRequested();
        EmitStepStopped(threadId);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken)
    {
        DisconnectCalled = true;
        cancellationToken.ThrowIfCancellationRequested();
        HasExited = true;
        return Task.CompletedTask;
    }

    public Task ForceKillAsync()
    {
        ForceKillCalled = true;
        HasExited = true;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        Interlocked.Exchange(ref _emitDisabled, 1);
        return ValueTask.CompletedTask;
    }

    public void AddStderr(string line) => _stderrLines.Add(line);

    public void SimulateProcessExit()
    {
        if (HasExited)
            return;

        HasExited = true;
        ProcessExited?.Invoke(Generation);
    }

    public void SimulateOutput(string text) =>
        Output?.Invoke(new DapOutputEvent(Generation, "stdout", text));

    public void SimulateStopped(string reason = "breakpoint", int threadId = 1) =>
        Stopped?.Invoke(new DapStoppedEvent(Generation, reason, threadId));

    private void EmitContinued(int threadId)
    {
        if (Volatile.Read(ref _emitDisabled) != 0)
            return;

        if (!DeferExecutionEvents)
        {
            Continued?.Invoke(new DapContinuedEvent(Generation, threadId));
            return;
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(ExecutionEventDelay).ConfigureAwait(false);
            if (Volatile.Read(ref _emitDisabled) != 0)
                return;

            Continued?.Invoke(new DapContinuedEvent(Generation, threadId));
        });
    }

    private void EmitStepStopped(int threadId)
    {
        if (Volatile.Read(ref _emitDisabled) != 0)
            return;

        if (!DeferExecutionEvents)
        {
            Stopped?.Invoke(new DapStoppedEvent(Generation, "step", threadId));
            return;
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(ExecutionEventDelay).ConfigureAwait(false);
            if (Volatile.Read(ref _emitDisabled) != 0)
                return;

            Stopped?.Invoke(new DapStoppedEvent(Generation, "step", threadId));
        });
    }

    public void SimulateTerminated() => Terminated?.Invoke(Generation);

    public void SimulateExited(int exitCode = 0) =>
        Exited?.Invoke(new DapExitedEvent(Generation, exitCode));
}
