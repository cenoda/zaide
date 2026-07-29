using System;
using System.Collections.Generic;

namespace Zaide.Features.Agents.Domain.Transparency.Trace;

/// <summary>
/// Read-side summary for one workspace partition's redacted trace evidence.
/// No PII or unredacted payload is returned. Used by the Agents presentation
/// availability projection and the inspection entry point.
/// </summary>
internal sealed class AgentTraceInspectionSummary
{
    public AgentTraceInspectionSummary(
        AgentDurableWorkspaceStorageKey workspaceKey,
        int totalRecords,
        long totalPayloadBytes,
        DateTimeOffset? oldestCapturedAtUtc,
        DateTimeOffset? newestCapturedAtUtc,
        IReadOnlyDictionary<AgentTraceCaptureState, int> countsByState,
        IReadOnlyDictionary<string, int> countsByBackend,
        bool isEmpty)
    {
        if (totalRecords < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalRecords),
                totalRecords,
                "Total records must be non-negative.");
        }

        if (totalPayloadBytes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalPayloadBytes),
                totalPayloadBytes,
                "Total payload bytes must be non-negative.");
        }

        WorkspaceKey = workspaceKey;
        TotalRecords = totalRecords;
        TotalPayloadBytes = totalPayloadBytes;
        OldestCapturedAtUtc = oldestCapturedAtUtc;
        NewestCapturedAtUtc = newestCapturedAtUtc;
        CountsByState = countsByState;
        CountsByBackend = countsByBackend;
        IsEmpty = isEmpty;
    }

    public AgentDurableWorkspaceStorageKey WorkspaceKey { get; }

    public int TotalRecords { get; }

    public long TotalPayloadBytes { get; }

    public DateTimeOffset? OldestCapturedAtUtc { get; }

    public DateTimeOffset? NewestCapturedAtUtc { get; }

    public IReadOnlyDictionary<AgentTraceCaptureState, int> CountsByState { get; }

    public IReadOnlyDictionary<string, int> CountsByBackend { get; }

    public bool IsEmpty { get; }

    public static AgentTraceInspectionSummary Empty(AgentDurableWorkspaceStorageKey workspaceKey) =>
        new(
            workspaceKey,
            totalRecords: 0,
            totalPayloadBytes: 0,
            oldestCapturedAtUtc: null,
            newestCapturedAtUtc: null,
            countsByState: new Dictionary<AgentTraceCaptureState, int>(),
            countsByBackend: new Dictionary<string, int>(StringComparer.Ordinal),
            isEmpty: true);
}
