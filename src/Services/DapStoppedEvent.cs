namespace Zaide.Services;

/// <summary>
/// Adapter <c>stopped</c> DAP event payload projected for session consumers.
/// </summary>
/// <param name="Generation">Session generation that received the event.</param>
/// <param name="Reason">Adapter-reported stop reason, when present.</param>
/// <param name="ThreadId">Stopped thread id, when present.</param>
public sealed record DapStoppedEvent(long Generation, string? Reason, int? ThreadId);
