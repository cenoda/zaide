using System;
using System.Collections.Generic;
using Zaide.Features.Agents.Domain.Transparency.Usage;

namespace Zaide.Features.Agents.Presentation.Transparency;

internal sealed class AgentUsageAvailabilityState
{
    public AgentUsageAvailabilityState(
        bool captureEnabled,
        int totalRecords,
        decimal totalCostValue,
        string? totalCostCurrency,
        DateTimeOffset? lastCapturedAtUtc,
        IReadOnlyDictionary<AgentUsageValueOrigin, int> countsByOrigin,
        bool hasVerifiedTotalCost = false)
    {
        CaptureEnabled = captureEnabled;
        TotalRecords = totalRecords;
        TotalCostValue = totalCostValue;
        TotalCostCurrency = totalCostCurrency;
        LastCapturedAtUtc = lastCapturedAtUtc;
        CountsByOrigin = countsByOrigin;
        HasVerifiedTotalCost = hasVerifiedTotalCost;
    }

    public bool CaptureEnabled { get; }

    public int TotalRecords { get; }

    public decimal TotalCostValue { get; }

    public string? TotalCostCurrency { get; }

    public bool HasVerifiedTotalCost { get; }

    public DateTimeOffset? LastCapturedAtUtc { get; }

    public IReadOnlyDictionary<AgentUsageValueOrigin, int> CountsByOrigin { get; }

    public string FormatStatusCaption()
    {
        if (!CaptureEnabled)
        {
            return "Usage capture disabled.";
        }

        var costPart = HasVerifiedTotalCost && TotalCostCurrency is not null
            ? $"{TotalCostValue:F4} {TotalCostCurrency} (not an invoice)"
            : "cost unavailable";
        return $"Usage capture enabled: {TotalRecords} record(s), {costPart}.";
    }

    public static AgentUsageAvailabilityState Initial { get; } = new(
        captureEnabled: false,
        totalRecords: 0,
        totalCostValue: 0,
        totalCostCurrency: null,
        lastCapturedAtUtc: null,
        countsByOrigin: new Dictionary<AgentUsageValueOrigin, int>(),
        hasVerifiedTotalCost: false);
}
