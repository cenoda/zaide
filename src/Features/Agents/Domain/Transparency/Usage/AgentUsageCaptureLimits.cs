namespace Zaide.Features.Agents.Domain.Transparency.Usage;

internal sealed class AgentUsageCaptureLimits
{
    public const int DefaultMaxRecordsPerPage = 128;

    public AgentUsageCaptureLimits(int maxRecordsPerPage = DefaultMaxRecordsPerPage)
    {
        if (maxRecordsPerPage <= 0)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(maxRecordsPerPage),
                maxRecordsPerPage,
                "Max records per page must be positive.");
        }

        MaxRecordsPerPage = maxRecordsPerPage;
    }

    public int MaxRecordsPerPage { get; }

    public static AgentUsageCaptureLimits Default { get; } = new();
}
