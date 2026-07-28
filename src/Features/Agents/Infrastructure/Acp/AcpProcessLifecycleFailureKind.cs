namespace Zaide.Features.Agents.Infrastructure.Acp;

/// <summary>
/// Distinguishes terminal ACP process lifecycle failures.
/// </summary>
internal enum AcpProcessLifecycleFailureKind
{
    Cancellation,
    Timeout,
    ProtocolFailure,
    ProcessExit,
    IndeterminateLateCompletion,
}
