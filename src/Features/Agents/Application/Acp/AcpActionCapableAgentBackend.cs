using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Application.Transparency;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Contracts.Transparency.Trace;
using Zaide.Features.Agents.Contracts.Transparency.Usage;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Trace;
using Zaide.Features.Agents.Domain.Transparency.Usage;
using Zaide.Features.Agents.Infrastructure.Acp;

namespace Zaide.Features.Agents.Application.Acp;

/// <summary>
/// ACP backend that advertises broker-mediated client filesystem capabilities.
/// </summary>
internal sealed class AcpActionCapableAgentBackend
    : IAgentBackend, IAgentActionRequestCapableBackend, IAgentCancellationAcknowledgementBackend
{
    private readonly AcpAgentSessionAdapter _sessionAdapter;
    private readonly IAgentTraceBackendEvidenceSource? _traceSource;
    private readonly IAgentUsageBackendEvidenceSource? _usageSource;
    private readonly AgentDurableWorkspaceStorageKeyResolver? _workspaceKeyResolver;
    private readonly Zaide.Features.Workspace.Contracts.IWorkspaceActionAuthority? _workspaceAuthority;
    private readonly object _capabilitySync = new();
    private AgentCapabilitySnapshot _capabilitySnapshot;
    private int _usageUpdateOrdinal;

    public AcpActionCapableAgentBackend(
        IAcpSessionClientFactory clientFactory,
        Func<string> workingDirectoryProvider,
        IAgentActorBackendBindingStore? bindingStore = null,
        Zaide.Features.Workspace.Contracts.IWorkspaceActionAuthority? workspaceAuthority = null,
        IAgentTraceBackendEvidenceSource? traceSource = null,
        AgentDurableWorkspaceStorageKeyResolver? workspaceKeyResolver = null,
        IAgentUsageBackendEvidenceSource? usageSource = null)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(workingDirectoryProvider);

        _sessionAdapter = new AcpAgentSessionAdapter(
            clientFactory,
            workingDirectoryProvider,
            bindingStore);
        _workspaceAuthority = workspaceAuthority;
        _traceSource = traceSource?.BackendId == AgentBackendIds.AcpValue ? traceSource : null;
        _usageSource = usageSource?.BackendId == AgentBackendIds.AcpValue ? usageSource : null;
        _workspaceKeyResolver = workspaceKeyResolver;
        _capabilitySnapshot = AcpCapabilitySnapshotMapper.CreateInitialSnapshot();
    }

    public AgentBackendId BackendId => AgentBackendIds.Acp;

    public string BackendVersion => AcpAgentBackend.BackendVersionValue;

    public AgentCapabilitySnapshot CapabilitySnapshot
    {
        get
        {
            lock (_capabilitySync)
            {
                return _capabilitySnapshot;
            }
        }
    }

    public async IAsyncEnumerable<AgentBackendEvent> ExecuteAsync(
        AgentBackendExecutionContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        AgentCapabilitySnapshot snapshot;
        lock (_capabilitySync)
        {
            snapshot = _capabilitySnapshot;
        }

        TryCaptureTrace(context, AgentTraceKind.Request, "session/prompt", "outbound", context.Request.MessageText);

        await foreach (var backendEvent in _sessionAdapter.ExecuteAsync(
                           context,
                           snapshot,
                           enableActionBridge: true,
                           cancellationToken).ConfigureAwait(false))
        {
            if (backendEvent.Kind == AgentBackendEventKind.CapabilitySnapshotChanged
                && backendEvent.Payload is AgentBackendCapabilityChangedPayload capabilityPayload)
            {
                lock (_capabilitySync)
                {
                    _capabilitySnapshot = capabilityPayload.Snapshot;
                }
            }

            TryCaptureTraceForEvent(context, backendEvent);
            TryCaptureUsageForEvent(context, backendEvent);

            yield return backendEvent;
        }
    }

    public Task<AgentCancellationAcknowledgementResult> AcknowledgeCancellationAsync(
        AgentSessionId sessionId,
        CancellationToken cancellationToken = default) =>
        _sessionAdapter.AcknowledgeCancellationAsync(sessionId, cancellationToken);

    private void TryCaptureTraceForEvent(
        AgentBackendExecutionContext context,
        AgentBackendEvent backendEvent)
    {
        switch (backendEvent.Payload)
        {
            case AgentBackendMessageCompletedPayload completed:
                TryCaptureTrace(context, AgentTraceKind.Response, "session/update", "inbound", completed.AssistantText);
                break;
            case AgentBackendFailurePayload failure:
                TryCaptureTrace(context, AgentTraceKind.Error, "session/error", "inbound", failure.Reason);
                break;
        }
    }

    private void TryCaptureUsageForEvent(
        AgentBackendExecutionContext context,
        AgentBackendEvent backendEvent)
    {
        if (backendEvent.Payload is not AgentBackendActivityReportedPayload activity
            || activity.ActivityKind != AcpBackendActivityKind.UsageUpdate
            || string.IsNullOrWhiteSpace(activity.UsageUpdateJson))
        {
            return;
        }

        TryCaptureAcpUsageUpdate(context, activity.UsageUpdateJson);
    }

    private void TryCaptureTrace(
        AgentBackendExecutionContext context,
        AgentTraceKind kind,
        string method,
        string direction,
        string opaqueBody)
    {
        if (_traceSource is null
            || _workspaceKeyResolver is null
            || _workspaceAuthority?.TryCaptureCurrentScope(out var workspaceScope) != true)
        {
            return;
        }

        var capturedAtUtc = DateTimeOffset.UtcNow;
        _ = _traceSource.Submit(new AgentTraceCaptureRequest(
            _workspaceKeyResolver.Resolve(workspaceScope.RootPath),
            AgentBackendIds.AcpValue,
            kind,
            AgentTraceEvidenceLevel.BackendExecutedAndReported,
            AcpAgentTraceSource.SerializeProtocolFrame(
                AgentBackendIds.AcpValue,
                method,
                context.Request.RunId.ToString(),
                direction,
                capturedAtUtc,
                AgentTraceBackendEvidenceSourceWriter.ComputeOpaqueBodyMarker(opaqueBody)),
            new AgentTraceRecordScope(
                context.Request.ConversationId.ToString(),
                context.Request.SessionId.ToString(),
                context.Request.RunId.ToString(),
                AgentBackendIds.AcpValue),
            idempotencyKey: $"trace:acp:{context.Request.RunId}:{method}",
            capturedAtUtc: capturedAtUtc));
    }

    /// <summary>
    /// Maps stable public ACP <c>usage_update</c> fields only:
    /// <c>used</c>/<c>size</c> are point-in-time context tokens; optional
    /// <c>cost.amount</c> is cumulative session cost. Never invents missing values.
    /// </summary>
    private void TryCaptureAcpUsageUpdate(
        AgentBackendExecutionContext context,
        string usageUpdateJson)
    {
        if (_usageSource is null
            || _workspaceKeyResolver is null
            || _workspaceAuthority?.TryCaptureCurrentScope(out var workspaceScope) != true)
        {
            return;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(usageUpdateJson);
        }
        catch (JsonException)
        {
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            if (!TryReadUInt64(root, "used", out var used)
                || !TryReadUInt64(root, "size", out var size))
            {
                return;
            }

            var workspaceKey = _workspaceKeyResolver.Resolve(workspaceScope.RootPath);
            var scope = new AgentUsageRecordScope(
                context.Request.ConversationId.ToString(),
                context.Request.SessionId.ToString(),
                context.Request.RunId.ToString(),
                AgentBackendIds.AcpValue);
            var capturedAtUtc = DateTimeOffset.UtcNow;
            var ordinal = System.Threading.Interlocked.Increment(ref _usageUpdateOrdinal);
            var runId = context.Request.RunId.ToString();

            _ = _usageSource.Submit(new AgentUsageCaptureRequest(
                workspaceKey,
                AgentBackendIds.AcpValue,
                AgentUsageKind.TotalTokens,
                AgentUsageValueOrigin.Reported,
                "context_tokens_used",
                "count",
                value: used,
                scope,
                evidenceSourceDescription:
                    "ACP usage_update.used — point-in-time context tokens (not input/output).",
                idempotencyKey: $"usage:acp:{runId}:used:{ordinal}",
                capturedAtUtc: capturedAtUtc,
                aggregationSemantics: AgentUsageAggregationSemantics.PointInTime));

            _ = _usageSource.Submit(new AgentUsageCaptureRequest(
                workspaceKey,
                AgentBackendIds.AcpValue,
                AgentUsageKind.Other,
                AgentUsageValueOrigin.Reported,
                "context_window_size",
                "count",
                value: size,
                scope,
                evidenceSourceDescription:
                    "ACP usage_update.size — point-in-time context window capacity.",
                idempotencyKey: $"usage:acp:{runId}:size:{ordinal}",
                capturedAtUtc: capturedAtUtc,
                aggregationSemantics: AgentUsageAggregationSemantics.PointInTime));

            if (root.TryGetProperty("cost", out var costElement)
                && costElement.ValueKind == JsonValueKind.Object
                && TryReadDecimal(costElement, "amount", out var amount)
                && costElement.TryGetProperty("currency", out var currencyElement)
                && currencyElement.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(currencyElement.GetString()))
            {
                var currency = currencyElement.GetString()!;
                _ = _usageSource.Submit(new AgentUsageCaptureRequest(
                    workspaceKey,
                    AgentBackendIds.AcpValue,
                    AgentUsageKind.TotalCost,
                    AgentUsageValueOrigin.Reported,
                    "session_cost",
                    currency,
                    value: amount,
                    scope,
                    currency: currency,
                    evidenceSourceDescription:
                        "ACP usage_update.cost — cumulative session cost (not an invoice).",
                    idempotencyKey: $"usage:acp:{runId}:cost:{ordinal}",
                    capturedAtUtc: capturedAtUtc,
                    aggregationSemantics: AgentUsageAggregationSemantics.Cumulative));
            }
        }
    }

    private static bool TryReadUInt64(JsonElement root, string propertyName, out decimal value)
    {
        value = 0;
        if (!root.TryGetProperty(propertyName, out var element))
        {
            return false;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetUInt64(out var number))
        {
            value = number;
            return true;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var decimalValue))
        {
            value = decimalValue;
            return true;
        }

        return false;
    }

    private static bool TryReadDecimal(JsonElement root, string propertyName, out decimal value)
    {
        value = 0;
        if (!root.TryGetProperty(propertyName, out var element))
        {
            return false;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var decimalValue))
        {
            value = decimalValue;
            return true;
        }

        if (element.ValueKind == JsonValueKind.String
            && decimal.TryParse(
                element.GetString(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out decimalValue))
        {
            value = decimalValue;
            return true;
        }

        return false;
    }
}
