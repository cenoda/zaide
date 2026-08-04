using System.Threading;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Process-wide invocation counters for isolated A3 agent-path evidence producers.
/// Incremented at real backend/broker/permission boundaries only; null in production
/// control flow when unset. Used by out-of-tree M4 force-quit evidence, not product UI.
/// </summary>
internal static class AgentPathEvidenceInvocationCounters
{
    private static int _nativeHarnessProviderRequests;
    private static int _acpSessionNewRequests;
    private static int _acpSessionPromptRequests;
    private static int _brokerRequests;
    private static int _permissionReviewRequests;

    public static int NativeHarnessProviderRequests =>
        Volatile.Read(ref _nativeHarnessProviderRequests);

    public static int AcpSessionNewRequests =>
        Volatile.Read(ref _acpSessionNewRequests);

    public static int AcpSessionPromptRequests =>
        Volatile.Read(ref _acpSessionPromptRequests);

    public static int BrokerRequests =>
        Volatile.Read(ref _brokerRequests);

    public static int PermissionReviewRequests =>
        Volatile.Read(ref _permissionReviewRequests);

    public static AgentPathEvidenceInvocationSnapshot Snapshot() =>
        new(
            NativeHarnessProviderRequests,
            AcpSessionNewRequests,
            AcpSessionPromptRequests,
            BrokerRequests,
            PermissionReviewRequests);

    internal static void RecordNativeHarnessProviderRequest() =>
        Interlocked.Increment(ref _nativeHarnessProviderRequests);

    internal static void RecordAcpSessionNewRequest() =>
        Interlocked.Increment(ref _acpSessionNewRequests);

    internal static void RecordAcpSessionPromptRequest() =>
        Interlocked.Increment(ref _acpSessionPromptRequests);

    internal static void RecordBrokerRequest() =>
        Interlocked.Increment(ref _brokerRequests);

    internal static void RecordPermissionReviewRequest() =>
        Interlocked.Increment(ref _permissionReviewRequests);
}

internal readonly record struct AgentPathEvidenceInvocationSnapshot(
    int NativeHarnessProviderRequests,
    int AcpSessionNewRequests,
    int AcpSessionPromptRequests,
    int BrokerRequests,
    int PermissionReviewRequests)
{
    public AgentPathEvidenceInvocationSnapshot Delta(AgentPathEvidenceInvocationSnapshot baseline) =>
        new(
            NativeHarnessProviderRequests - baseline.NativeHarnessProviderRequests,
            AcpSessionNewRequests - baseline.AcpSessionNewRequests,
            AcpSessionPromptRequests - baseline.AcpSessionPromptRequests,
            BrokerRequests - baseline.BrokerRequests,
            PermissionReviewRequests - baseline.PermissionReviewRequests);
}
