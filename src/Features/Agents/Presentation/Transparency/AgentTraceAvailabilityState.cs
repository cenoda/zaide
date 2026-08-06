using System;
using System.Collections.Generic;
using Zaide.Features.Agents.Domain.Transparency.Trace;

namespace Zaide.Features.Agents.Presentation.Transparency;

/// <summary>
/// Observable state published by <see cref="AgentTraceAvailabilityProjection"/>.
/// The presentation layer is read-only; the projection never mutates the
/// underlying trace records or the agent event pipeline. The inspection
/// entry point receives this state plus the source registry and inspector.
/// </summary>
internal sealed class AgentTraceAvailabilityState
{
    public AgentTraceAvailabilityState(
        bool captureEnabled,
        int totalRecords,
        long totalPayloadBytes,
        DateTimeOffset? lastCapturedAtUtc,
        IReadOnlyDictionary<AgentTraceCaptureState, int> countsByState,
        bool backpressureObserved)
    {
        CaptureEnabled = captureEnabled;
        TotalRecords = totalRecords;
        TotalPayloadBytes = totalPayloadBytes;
        LastCapturedAtUtc = lastCapturedAtUtc;
        CountsByState = countsByState;
        BackpressureObserved = backpressureObserved;
    }

    public bool CaptureEnabled { get; }

    public int TotalRecords { get; }

    public long TotalPayloadBytes { get; }

    public DateTimeOffset? LastCapturedAtUtc { get; }

    public IReadOnlyDictionary<AgentTraceCaptureState, int> CountsByState { get; }

    public bool BackpressureObserved { get; }

    public string FormatStatusCaption() =>
        CaptureEnabled
            ? $"Trace capture enabled: {TotalRecords} record(s), {TotalPayloadBytes} byte(s)."
            : "Capture off — change in Settings.";

    public static AgentTraceAvailabilityState Initial { get; } = new(
        captureEnabled: false,
        totalRecords: 0,
        totalPayloadBytes: 0,
        lastCapturedAtUtc: null,
        countsByState: new Dictionary<AgentTraceCaptureState, int>(),
        backpressureObserved: false);
}
