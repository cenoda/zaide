namespace Zaide.Services;

/// <summary>
/// Immutable stopped-thread details from the most recent <c>stopped</c> DAP event.
/// </summary>
/// <param name="Reason">Adapter-reported stop reason, when present.</param>
/// <param name="ThreadId">Stopped thread id, when present.</param>
public sealed record DapStoppedInfo(string? Reason, int? ThreadId);
