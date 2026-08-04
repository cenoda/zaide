using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Contracts.Transparency.Usage;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Usage;

namespace Zaide.Features.Agents.Application.Transparency.Usage;

internal sealed class AgentUsageInspector : IAgentUsageInspector
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly IAgentDurableRecordStore _store;

    public AgentUsageInspector(IAgentDurableRecordStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public AgentUsageInspectionSummary GetSummary(AgentDurableWorkspaceStorageKey workspaceKey)
    {
        var records = ReplayAll(workspaceKey);
        if (records.Count == 0)
        {
            return AgentUsageInspectionSummary.Empty(workspaceKey);
        }

        var countsByOrigin = new Dictionary<AgentUsageValueOrigin, int>();
        var countsByBackend = new Dictionary<string, int>(StringComparer.Ordinal);
        DateTimeOffset? oldest = null;
        DateTimeOffset? newest = null;

        // Latest cumulative cost snapshot per backend/session/currency.
        var latestCumulativeCost = new Dictionary<string, AgentUsageRecord>(StringComparer.Ordinal);
        decimal deltaCostTotal = 0;
        string? verifiedCurrency = null;
        var hasVerifiedContribution = false;
        var mixedCurrency = false;

        foreach (var record in records)
        {
            if (!countsByOrigin.TryGetValue(record.Origin, out var originCount))
            {
                originCount = 0;
            }

            countsByOrigin[record.Origin] = originCount + 1;

            if (!countsByBackend.TryGetValue(record.BackendId, out var backendCount))
            {
                backendCount = 0;
            }

            countsByBackend[record.BackendId] = backendCount + 1;

            if (IsCostKind(record.Kind) && record.Origin != AgentUsageValueOrigin.Unavailable)
            {
                switch (record.AggregationSemantics)
                {
                    case AgentUsageAggregationSemantics.Delta:
                        if (!TryAcceptCurrency(record.Currency, ref verifiedCurrency, ref mixedCurrency))
                        {
                            break;
                        }

                        deltaCostTotal += record.Value;
                        hasVerifiedContribution = true;
                        break;

                    case AgentUsageAggregationSemantics.Cumulative:
                        if (!TryAcceptCurrency(record.Currency, ref verifiedCurrency, ref mixedCurrency))
                        {
                            break;
                        }

                        var key = BuildCumulativeKey(record);
                        if (!latestCumulativeCost.TryGetValue(key, out var existing)
                            || record.OrderingSequence > existing.OrderingSequence)
                        {
                            latestCumulativeCost[key] = record;
                        }

                        hasVerifiedContribution = true;
                        break;

                    case AgentUsageAggregationSemantics.Unknown:
                    case AgentUsageAggregationSemantics.PointInTime:
                    default:
                        // Listed in records but excluded from a verified cost aggregate.
                        break;
                }
            }

            if (oldest is null || record.CapturedAtUtc < oldest)
            {
                oldest = record.CapturedAtUtc;
            }

            if (newest is null || record.CapturedAtUtc > newest)
            {
                newest = record.CapturedAtUtc;
            }
        }

        decimal cumulativeCostTotal = 0;
        foreach (var latest in latestCumulativeCost.Values)
        {
            cumulativeCostTotal += latest.Value;
        }

        var hasVerifiedTotalCost = hasVerifiedContribution && !mixedCurrency;
        var totalCostValue = hasVerifiedTotalCost
            ? deltaCostTotal + cumulativeCostTotal
            : 0m;

        return new AgentUsageInspectionSummary(
            workspaceKey,
            totalRecords: records.Count,
            totalCostValue: totalCostValue,
            totalCostCurrency: hasVerifiedTotalCost ? verifiedCurrency : null,
            oldestCapturedAtUtc: oldest,
            newestCapturedAtUtc: newest,
            countsByOrigin: countsByOrigin,
            countsByBackend: countsByBackend,
            isEmpty: false,
            hasVerifiedTotalCost: hasVerifiedTotalCost);
    }

    public IReadOnlyList<AgentUsageRecord> GetRecords(
        AgentDurableWorkspaceStorageKey workspaceKey,
        long afterOrderingSequence,
        int maxRecords)
    {
        if (maxRecords <= 0)
        {
            return Array.Empty<AgentUsageRecord>();
        }

        var replay = _store.Replay(new AgentDurableRecordReplayRequest(
            workspaceKey,
            AgentDurableRecordClass.Usage,
            afterOrderingSequence,
            maxRecords));

        var projected = new List<AgentUsageRecord>(replay.Records.Count);
        foreach (var envelope in replay.Records)
        {
            if (TryDecode(envelope, out var record))
            {
                projected.Add(record);
            }
        }

        return projected;
    }

    private IReadOnlyList<AgentUsageRecord> ReplayAll(AgentDurableWorkspaceStorageKey workspaceKey)
    {
        const int pageSize = 256;
        long cursor = 0;
        var collected = new List<AgentUsageRecord>();

        while (true)
        {
            var page = GetRecords(workspaceKey, cursor, pageSize);
            if (page.Count == 0)
            {
                break;
            }

            collected.AddRange(page);
            cursor = page[^1].OrderingSequence;
            if (page.Count < pageSize)
            {
                break;
            }
        }

        return collected;
    }

    private static bool IsCostKind(AgentUsageKind kind) =>
        kind is AgentUsageKind.EstimatedCost
            or AgentUsageKind.InvoicedCost
            or AgentUsageKind.TotalCost;

    private static string BuildCumulativeKey(AgentUsageRecord record)
    {
        var session = record.Scope.SessionId ?? string.Empty;
        var currency = record.Currency ?? string.Empty;
        return record.BackendId + "|" + session + "|" + currency;
    }

    private static bool TryAcceptCurrency(
        string? currency,
        ref string? verifiedCurrency,
        ref bool mixedCurrency)
    {
        if (mixedCurrency)
        {
            return false;
        }

        if (verifiedCurrency is null)
        {
            verifiedCurrency = currency;
            return true;
        }

        if (string.Equals(verifiedCurrency, currency, StringComparison.Ordinal))
        {
            return true;
        }

        mixedCurrency = true;
        return false;
    }

    private static bool TryDecode(
        AgentDurableRecordEnvelope envelope,
        out AgentUsageRecord record)
    {
        record = null!;
        try
        {
            var payload = JsonSerializer.Deserialize<UsageRecordJson>(
                envelope.PayloadJson,
                SerializerOptions);
            if (payload is null)
            {
                return false;
            }

            // Additive field: missing payload property deserializes as default Unknown.
            var aggregation = Enum.IsDefined(payload.AggregationSemantics)
                ? payload.AggregationSemantics
                : AgentUsageAggregationSemantics.Unknown;

            record = new AgentUsageRecord(
                envelope.RecordId.Value,
                envelope.OrderingSequence,
                payload.BackendId ?? envelope.ScopeReferences.BackendId ?? "unknown",
                payload.Kind,
                payload.Origin,
                payload.MetricName ?? "unknown",
                payload.Unit ?? "count",
                payload.Value,
                new AgentUsageRecordScope(
                    envelope.ScopeReferences.ConversationId,
                    envelope.ScopeReferences.SessionId,
                    envelope.ScopeReferences.RunId,
                    envelope.ScopeReferences.BackendId),
                model: payload.Model,
                pricingSourceId: payload.PricingSourceId,
                pricingSourceVersion: payload.PricingSourceVersion,
                pricingFormula: payload.PricingFormula,
                currency: payload.Currency,
                pricingEffectiveTime: payload.PricingEffectiveTime,
                roundingDecimals: payload.RoundingDecimals,
                uncertainty: payload.Uncertainty,
                evidenceSourceDescription: payload.EvidenceSourceDescription,
                capturedAtUtc: payload.CapturedAtUtc,
                recordedAtUtc: envelope.RecordedAtUtc,
                aggregationSemantics: aggregation);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed class UsageRecordJson
    {
        public string BackendId { get; set; } = string.Empty;

        public AgentUsageKind Kind { get; set; }

        public AgentUsageValueOrigin Origin { get; set; }

        public string MetricName { get; set; } = string.Empty;

        public string Unit { get; set; } = string.Empty;

        public decimal Value { get; set; }

        public string? Model { get; set; }

        public string? PricingSourceId { get; set; }

        public int? PricingSourceVersion { get; set; }

        public string? PricingFormula { get; set; }

        public string? Currency { get; set; }

        public DateTimeOffset? PricingEffectiveTime { get; set; }

        public int? RoundingDecimals { get; set; }

        public decimal? Uncertainty { get; set; }

        public string? EvidenceSourceDescription { get; set; }

        public DateTimeOffset CapturedAtUtc { get; set; }

        public AgentUsageAggregationSemantics AggregationSemantics { get; set; }
    }
}
