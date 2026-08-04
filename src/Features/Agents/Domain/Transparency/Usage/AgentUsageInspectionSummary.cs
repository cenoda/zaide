using System;
using System.Collections.Generic;

namespace Zaide.Features.Agents.Domain.Transparency.Usage;

internal sealed class AgentUsageInspectionSummary
{
    public AgentUsageInspectionSummary(
        AgentDurableWorkspaceStorageKey workspaceKey,
        int totalRecords,
        decimal totalCostValue,
        string? totalCostCurrency,
        DateTimeOffset? oldestCapturedAtUtc,
        DateTimeOffset? newestCapturedAtUtc,
        IReadOnlyDictionary<AgentUsageValueOrigin, int> countsByOrigin,
        IReadOnlyDictionary<string, int> countsByBackend,
        bool isEmpty,
        bool hasVerifiedTotalCost = false)
    {
        if (totalRecords < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalRecords),
                totalRecords,
                "Total records must be non-negative.");
        }

        WorkspaceKey = workspaceKey;
        TotalRecords = totalRecords;
        TotalCostValue = totalCostValue;
        TotalCostCurrency = totalCostCurrency;
        OldestCapturedAtUtc = oldestCapturedAtUtc;
        NewestCapturedAtUtc = newestCapturedAtUtc;
        CountsByOrigin = countsByOrigin;
        CountsByBackend = countsByBackend;
        IsEmpty = isEmpty;
        HasVerifiedTotalCost = hasVerifiedTotalCost;
    }

    public AgentDurableWorkspaceStorageKey WorkspaceKey { get; }

    public int TotalRecords { get; }

    public decimal TotalCostValue { get; }

    public string? TotalCostCurrency { get; }

    /// <summary>
    /// True when <see cref="TotalCostValue"/> is a verified aggregate of Delta
    /// and latest Cumulative cost records. Missing, Unknown, or mixed-currency
    /// evidence must not be treated as verified zero.
    /// </summary>
    public bool HasVerifiedTotalCost { get; }

    public DateTimeOffset? OldestCapturedAtUtc { get; }

    public DateTimeOffset? NewestCapturedAtUtc { get; }

    public IReadOnlyDictionary<AgentUsageValueOrigin, int> CountsByOrigin { get; }

    public IReadOnlyDictionary<string, int> CountsByBackend { get; }

    public bool IsEmpty { get; }

    public static AgentUsageInspectionSummary Empty(AgentDurableWorkspaceStorageKey workspaceKey) =>
        new(
            workspaceKey,
            totalRecords: 0,
            totalCostValue: 0,
            totalCostCurrency: null,
            oldestCapturedAtUtc: null,
            newestCapturedAtUtc: null,
            countsByOrigin: new Dictionary<AgentUsageValueOrigin, int>(),
            countsByBackend: new Dictionary<string, int>(StringComparer.Ordinal),
            isEmpty: true,
            hasVerifiedTotalCost: false);
}
