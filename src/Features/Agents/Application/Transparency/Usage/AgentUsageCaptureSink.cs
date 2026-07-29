using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Contracts.Transparency.Usage;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Usage;

namespace Zaide.Features.Agents.Application.Transparency.Usage;

internal sealed class AgentUsageCaptureSink : IAgentUsageCaptureSink
{
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly AgentUsageCaptureLimits _limits;
    private readonly IAgentDurableRecordStore _store;
    private int _captureEnabledCounter;

    public AgentUsageCaptureSink(
        AgentUsageCaptureLimits limits,
        IAgentDurableRecordStore store)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(store);

        _limits = limits;
        _store = store;
    }

    public AgentUsageCaptureLimits Limits => _limits;

    public bool IsCaptureEnabled() => Volatile.Read(ref _captureEnabledCounter) > 0;

    public void EnableCapture() => Interlocked.Increment(ref _captureEnabledCounter);

    public void DisableCapture() => Interlocked.Exchange(ref _captureEnabledCounter, 0);

    public AgentUsageCaptureResult TrySubmit(AgentUsageCaptureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsCaptureEnabled())
        {
            return new AgentUsageCaptureResult(
                AgentUsageCaptureStatus.Disabled,
                reason: "Usage capture is disabled.");
        }

        if (request.Origin == AgentUsageValueOrigin.Unavailable)
        {
            return Admit(request);
        }

        if (request.Value == 0 && request.Kind != AgentUsageKind.Other)
        {
            var isCostKind = request.Kind is AgentUsageKind.EstimatedCost
                or AgentUsageKind.InvoicedCost
                or AgentUsageKind.TotalCost;
            if (isCostKind)
            {
                return new AgentUsageCaptureResult(
                    AgentUsageCaptureStatus.InvalidRequest,
                    reason: "Cost value must not be zero when origin is not Unavailable. Use Unavailable origin for missing cost evidence.");
            }
        }

        return Admit(request);
    }

    private AgentUsageCaptureResult Admit(AgentUsageCaptureRequest request)
    {
        var envelope = new UsageRecordPayload
        {
            BackendId = request.BackendId,
            Kind = request.Kind,
            Origin = request.Origin,
            MetricName = request.MetricName,
            Unit = request.Unit,
            Value = request.Value,
            Model = request.Model,
            PricingSourceId = request.PricingSourceId,
            PricingSourceVersion = request.PricingSourceVersion,
            PricingFormula = request.PricingFormula,
            Currency = request.Currency,
            PricingEffectiveTime = request.PricingEffectiveTime,
            RoundingDecimals = request.RoundingDecimals,
            Uncertainty = request.Uncertainty,
            EvidenceSourceDescription = request.EvidenceSourceDescription,
            CapturedAtUtc = request.CapturedAtUtc,
        };

        var payloadJson = JsonSerializer.Serialize(envelope, PayloadOptions);
        var idempotencyKey = request.IdempotencyKey
            ?? BuildIdempotencyKey(request);

        var appendRequest = new AgentDurableRecordAppendRequest(
            request.WorkspaceKey,
            AgentDurableRecordClass.Usage,
            idempotencyKey: idempotencyKey,
            payloadJson: payloadJson,
            scopeReferences: new AgentDurableRecordScopeReferences(
                conversationId: request.Scope.ConversationId,
                sessionId: request.Scope.SessionId,
                runId: request.Scope.RunId,
                backendId: request.BackendId),
            recordedAtUtc: request.CapturedAtUtc);

        var result = _store.TryAppend(appendRequest);

        if (result.Status == AgentDurableRecordAppendStatus.DuplicateIgnored)
        {
            return new AgentUsageCaptureResult(
                AgentUsageCaptureStatus.DuplicateIgnored,
                reason: "Idempotent duplicate ignored.");
        }

        if (result.Status != AgentDurableRecordAppendStatus.Appended)
        {
            return new AgentUsageCaptureResult(
                AgentUsageCaptureStatus.InvalidRequest,
                reason: $"Store rejected append: {result.Status}");
        }

        return new AgentUsageCaptureResult(
            AgentUsageCaptureStatus.Accepted,
            orderingSequence: result.Envelope?.OrderingSequence ?? 0);
    }

    private static string BuildIdempotencyKey(AgentUsageCaptureRequest request)
    {
        var raw = string.Join(
            "|",
            request.WorkspaceKey.Value,
            request.BackendId,
            request.Kind.ToString(),
            request.MetricName,
            request.Unit,
            request.CapturedAtUtc.UtcTicks.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
        return "usage:" + Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                Encoding.UTF8.GetBytes(raw)))[..16];
    }

    private sealed class UsageRecordPayload
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
    }
}
