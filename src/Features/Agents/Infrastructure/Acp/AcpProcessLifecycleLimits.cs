using System;

namespace Zaide.Features.Agents.Infrastructure.Acp;

/// <summary>
/// Bounded process lifecycle budgets for ACP stdio hosting.
/// </summary>
internal static class AcpProcessLifecycleLimits
{
    public static TimeSpan InitializeTimeout { get; internal set; } = TimeSpan.FromSeconds(30);

    public static TimeSpan SessionOperationTimeout { get; internal set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Independent bounded budget for <c>session/cancel</c> after the run token is cancelled.
    /// Must not reuse the already-cancelled run token.
    /// </summary>
    public static TimeSpan CancelPromptTimeout { get; internal set; } = TimeSpan.FromSeconds(5);

    public static readonly TimeSpan ProcessTreeCleanupTimeout = TimeSpan.FromSeconds(5);

    public const int MaxStderrBytes = 64 * 1024;

    public const int MaxStderrLineBytes = 16 * 1024;
}
