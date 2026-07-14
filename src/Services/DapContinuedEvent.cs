namespace Zaide.Services;

/// <summary>
/// Adapter <c>continued</c> DAP event payload projected for session consumers.
/// </summary>
/// <param name="Generation">Session generation that received the event.</param>
/// <param name="ThreadId">Continued thread id, when present.</param>
public sealed record DapContinuedEvent(long Generation, int? ThreadId);
