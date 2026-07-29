using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Contracts.Transparency.Trace;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Trace;

namespace Zaide.Features.Agents.Application.Transparency.Trace;

/// <summary>
/// Read-side inspection over the M1 Trace record class. Decodes the stored
/// payload (always post-redaction) into <see cref="AgentTraceRecord"/>
/// projections; never re-reads the original input payload.
/// </summary>
internal sealed class AgentTraceInspector : IAgentTraceInspector
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IAgentDurableRecordStore _store;

    public AgentTraceInspector(IAgentDurableRecordStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public AgentTraceInspectionSummary GetSummary(AgentDurableWorkspaceStorageKey workspaceKey)
    {
        ArgumentNullException.ThrowIfNull(workspaceKey);

        var records = ReplayAll(workspaceKey);
        if (records.Count == 0)
        {
            return AgentTraceInspectionSummary.Empty(workspaceKey);
        }

        var countsByState = new Dictionary<AgentTraceCaptureState, int>();
        var countsByBackend = new Dictionary<string, int>(StringComparer.Ordinal);
        long totalBytes = 0;
        DateTimeOffset? oldest = null;
        DateTimeOffset? newest = null;

        foreach (var record in records)
        {
            if (!countsByState.TryGetValue(record.CaptureState, out var stateCount))
            {
                stateCount = 0;
            }

            countsByState[record.CaptureState] = stateCount + 1;

            if (!countsByBackend.TryGetValue(record.BackendId, out var backendCount))
            {
                backendCount = 0;
            }

            countsByBackend[record.BackendId] = backendCount + 1;

            totalBytes += record.PayloadByteCount;

            if (oldest is null || record.CapturedAtUtc < oldest)
            {
                oldest = record.CapturedAtUtc;
            }

            if (newest is null || record.CapturedAtUtc > newest)
            {
                newest = record.CapturedAtUtc;
            }
        }

        return new AgentTraceInspectionSummary(
            workspaceKey,
            totalRecords: records.Count,
            totalPayloadBytes: totalBytes,
            oldestCapturedAtUtc: oldest,
            newestCapturedAtUtc: newest,
            countsByState: countsByState,
            countsByBackend: countsByBackend,
            isEmpty: false);
    }

    public IReadOnlyList<AgentTraceRecord> GetRecords(
        AgentDurableWorkspaceStorageKey workspaceKey,
        long afterOrderingSequence,
        int maxRecords)
    {
        ArgumentNullException.ThrowIfNull(workspaceKey);

        if (maxRecords <= 0)
        {
            return Array.Empty<AgentTraceRecord>();
        }

        var replay = _store.Replay(new AgentDurableRecordReplayRequest(
            workspaceKey,
            AgentDurableRecordClass.Trace,
            afterOrderingSequence,
            maxRecords));

        var projected = new List<AgentTraceRecord>(replay.Records.Count);
        foreach (var envelope in replay.Records)
        {
            if (TryDecode(envelope, out var trace))
            {
                projected.Add(trace);
            }
        }

        return projected;
    }

    private IReadOnlyList<AgentTraceRecord> ReplayAll(AgentDurableWorkspaceStorageKey workspaceKey)
    {
        const int pageSize = 256;
        long cursor = 0;
        var collected = new List<AgentTraceRecord>();

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

    private static bool TryDecode(
        AgentDurableRecordEnvelope envelope,
        out AgentTraceRecord record)
    {
        record = null!;
        try
        {
            var payload = JsonSerializer.Deserialize<TraceRecordJson>(
                envelope.PayloadJson,
                SerializerOptions);
            if (payload is null)
            {
                return false;
            }

            record = new AgentTraceRecord(
                envelope.RecordId.Value,
                envelope.OrderingSequence,
                payload.BackendId ?? envelope.ScopeReferences.BackendId ?? "unknown",
                payload.Kind,
                payload.EvidenceLevel,
                payload.CaptureState,
                payload.RedactedPayload ?? envelope.PayloadJson,
                payload.PayloadByteCount,
                new AgentTraceRecordScope(
                    envelope.ScopeReferences.ConversationId,
                    envelope.ScopeReferences.SessionId,
                    envelope.ScopeReferences.RunId,
                    envelope.ScopeReferences.BackendId),
                payload.CapturedAtUtc,
                envelope.RecordedAtUtc,
                payload.RedactionReason);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Serialized trace payload shape stored in the M1 Trace record class.
    /// Field names are camelCase to match the rest of the codebase.
    /// </summary>
    private sealed class TraceRecordJson
    {
        public string BackendId { get; set; } = string.Empty;

        public AgentTraceKind Kind { get; set; }

        public AgentTraceEvidenceLevel EvidenceLevel { get; set; }

        public AgentTraceCaptureState CaptureState { get; set; }

        public string? RedactedPayload { get; set; }

        public int PayloadByteCount { get; set; }

        public DateTimeOffset CapturedAtUtc { get; set; }

        public string? RedactionReason { get; set; }
    }
}
