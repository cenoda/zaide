using System;

namespace Zaide.Features.Agents.Domain.Transparency.Usage;

internal sealed class AgentUsageCaptureRequest
{
    public AgentUsageCaptureRequest(
        AgentDurableWorkspaceStorageKey workspaceKey,
        string backendId,
        AgentUsageKind kind,
        AgentUsageValueOrigin origin,
        string metricName,
        string unit,
        decimal value,
        AgentUsageRecordScope scope,
        string? model = null,
        string? pricingSourceId = null,
        int? pricingSourceVersion = null,
        string? pricingFormula = null,
        string? currency = null,
        DateTimeOffset? pricingEffectiveTime = null,
        int? roundingDecimals = null,
        decimal? uncertainty = null,
        string? evidenceSourceDescription = null,
        string? idempotencyKey = null,
        DateTimeOffset? capturedAtUtc = null,
        AgentUsageAggregationSemantics aggregationSemantics = AgentUsageAggregationSemantics.Unknown)
    {
        if (workspaceKey.Value.Length == 0)
        {
            throw new ArgumentException("Workspace key is required.", nameof(workspaceKey));
        }

        if (string.IsNullOrWhiteSpace(backendId))
        {
            throw new ArgumentException("Backend id is required.", nameof(backendId));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Usage kind is invalid.");
        }

        if (!Enum.IsDefined(origin))
        {
            throw new ArgumentOutOfRangeException(nameof(origin), origin, "Value origin is invalid.");
        }

        if (!Enum.IsDefined(aggregationSemantics))
        {
            throw new ArgumentOutOfRangeException(
                nameof(aggregationSemantics),
                aggregationSemantics,
                "Aggregation semantics are invalid.");
        }

        if (string.IsNullOrWhiteSpace(metricName))
        {
            throw new ArgumentException("Metric name is required.", nameof(metricName));
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new ArgumentException("Unit is required.", nameof(unit));
        }

        if (origin != AgentUsageValueOrigin.Unavailable && value == 0 && kind != AgentUsageKind.Other)
        {
        }

        WorkspaceKey = workspaceKey;
        BackendId = backendId;
        Kind = kind;
        Origin = origin;
        MetricName = metricName;
        Unit = unit;
        Value = value;
        Model = model;
        Scope = scope;
        PricingSourceId = pricingSourceId;
        PricingSourceVersion = pricingSourceVersion;
        PricingFormula = pricingFormula;
        Currency = currency;
        PricingEffectiveTime = pricingEffectiveTime;
        RoundingDecimals = roundingDecimals;
        Uncertainty = uncertainty;
        EvidenceSourceDescription = evidenceSourceDescription;
        IdempotencyKey = idempotencyKey;
        CapturedAtUtc = capturedAtUtc ?? DateTimeOffset.UtcNow;
        AggregationSemantics = aggregationSemantics;
    }

    public AgentDurableWorkspaceStorageKey WorkspaceKey { get; }

    public string BackendId { get; }

    public AgentUsageKind Kind { get; }

    public AgentUsageValueOrigin Origin { get; }

    public string MetricName { get; }

    public string Unit { get; }

    public decimal Value { get; }

    public string? Model { get; }

    public AgentUsageRecordScope Scope { get; }

    public string? PricingSourceId { get; }

    public int? PricingSourceVersion { get; }

    public string? PricingFormula { get; }

    public string? Currency { get; }

    public DateTimeOffset? PricingEffectiveTime { get; }

    public int? RoundingDecimals { get; }

    public decimal? Uncertainty { get; }

    public string? EvidenceSourceDescription { get; }

    public string? IdempotencyKey { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public AgentUsageAggregationSemantics AggregationSemantics { get; }
}
