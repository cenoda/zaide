namespace Zaide.Features.Agents.Domain.Transparency.Usage;

/// <summary>
/// How a usage or cost value should be aggregated for inspection.
/// Existing records without this field decode as <see cref="Unknown"/>.
/// </summary>
internal enum AgentUsageAggregationSemantics
{
    /// <summary>Legacy or unspecified; listed but excluded from verified cost totals.</summary>
    Unknown = 0,

    /// <summary>Per-run or per-event delta; cost deltas may be summed.</summary>
    Delta = 1,

    /// <summary>Session-cumulative snapshot; only the latest per backend/session/currency is kept.</summary>
    Cumulative = 2,

    /// <summary>Point-in-time observation (e.g. context tokens used); not summed as a delta.</summary>
    PointInTime = 3,
}
