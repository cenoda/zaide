namespace Zaide.Features.Agents.Domain.Transparency.Trace;

/// <summary>
/// M2 trace capture limits. Enforced by <see cref="AgentTraceBoundedCaptureQueue"/>
/// and the capture sink before any durable write. Override only through
/// approved composition root wiring; never at runtime per request.
/// </summary>
internal sealed class AgentTraceCaptureLimits
{
    public const int DefaultMaxPayloadBytes = 64 * 1024;

    public const int DefaultMaxQueueDepth = 256;

    public const int DefaultMaxRecordsPerPage = 128;

    public AgentTraceCaptureLimits(
        int maxPayloadBytes = DefaultMaxPayloadBytes,
        int maxQueueDepth = DefaultMaxQueueDepth,
        int maxRecordsPerPage = DefaultMaxRecordsPerPage)
    {
        if (maxPayloadBytes <= 0)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(maxPayloadBytes),
                maxPayloadBytes,
                "Max payload bytes must be positive.");
        }

        if (maxQueueDepth <= 0)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(maxQueueDepth),
                maxQueueDepth,
                "Max queue depth must be positive.");
        }

        if (maxRecordsPerPage <= 0)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(maxRecordsPerPage),
                maxRecordsPerPage,
                "Max records per page must be positive.");
        }

        MaxPayloadBytes = maxPayloadBytes;
        MaxQueueDepth = maxQueueDepth;
        MaxRecordsPerPage = maxRecordsPerPage;
    }

    public int MaxPayloadBytes { get; }

    public int MaxQueueDepth { get; }

    public int MaxRecordsPerPage { get; }

    public static AgentTraceCaptureLimits Default { get; } = new();
}
