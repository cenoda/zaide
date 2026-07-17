using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Debugging.Application;

namespace Zaide.Features.Debugging.Contracts;

/// <summary>
/// Singleton service that owns one NetCoreDbg adapter session and DAP lifecycle state.
/// </summary>
public interface IDebugSessionService : IDisposable
{
    /// <summary>The current immutable debug-session snapshot.</summary>
    DebugSessionSnapshot Current { get; }

    /// <summary>
    /// Emits each new <see cref="DebugSessionSnapshot"/> on the calling thread.
    /// </summary>
    IObservable<DebugSessionSnapshot> WhenChanged { get; }

    /// <summary>
    /// Starts one launch-debug session using explicit launch parameters.
    /// Returns <see cref="DebugSessionOutcomeKind.RejectedConcurrent"/> when a session is already active.
    /// </summary>
    Task<DebugSessionOperationResult> StartLaunchAsync(
        DebugLaunchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a terminal pre-launch failure while no adapter session is active
    /// (build failure, target resolution, etc.). Idempotent no-op when a session
    /// is already active. Leaves F5 startable from <see cref="DebugSessionState.Failed"/>.
    /// </summary>
    Task<DebugSessionOperationResult> ReportPreLaunchFailureAsync(
        DebugSessionOutcomeKind kind,
        string message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects the active session. Idempotent when no session is active.
    /// </summary>
    Task<DebugSessionOperationResult> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues <c>continue</c> for one thread while the session is stopped.
    /// </summary>
    Task<DebugSessionOperationResult> ContinueAsync(
        int threadId,
        CancellationToken cancellationToken = default);

    /// <summary>Issues <c>pause</c> while the session is running.</summary>
    Task<DebugSessionOperationResult> PauseAsync(CancellationToken cancellationToken = default);

    /// <summary>Issues <c>next</c> while the session is stopped.</summary>
    Task<DebugSessionOperationResult> StepOverAsync(CancellationToken cancellationToken = default);

    /// <summary>Issues <c>stepIn</c> while the session is stopped.</summary>
    Task<DebugSessionOperationResult> StepIntoAsync(CancellationToken cancellationToken = default);

    /// <summary>Issues <c>stepOut</c> while the session is stopped.</summary>
    Task<DebugSessionOperationResult> StepOutAsync(CancellationToken cancellationToken = default);

    /// <summary>Issues <c>threads</c> while the session is stopped.</summary>
    Task<JsonElement?> RequestThreadsAsync(CancellationToken cancellationToken = default);

    /// <summary>Issues <c>stackTrace</c> while the session is stopped.</summary>
    Task<JsonElement?> RequestStackTraceAsync(
        int threadId,
        CancellationToken cancellationToken = default);

    /// <summary>Issues <c>scopes</c> while the session is stopped.</summary>
    Task<JsonElement?> RequestScopesAsync(
        int frameId,
        CancellationToken cancellationToken = default);

    /// <summary>Issues <c>variables</c> while the session is stopped.</summary>
    Task<JsonElement?> RequestVariablesAsync(
        int variablesReference,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces persisted breakpoints for each source path while a configured
    /// debug session is running or stopped. No-ops when no adapter session is active.
    /// </summary>
    Task<DebugSessionOperationResult> ReplaceBreakpointsBySourceAsync(
        IReadOnlyDictionary<string, IReadOnlyList<int>> replacementBySource,
        CancellationToken cancellationToken = default);
}
