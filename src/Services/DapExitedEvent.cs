namespace Zaide.Services;

/// <summary>
/// Adapter <c>exited</c> DAP event payload projected for session consumers.
/// </summary>
/// <param name="Generation">Session generation that received the event.</param>
/// <param name="ExitCode">Debuggee exit code when present.</param>
public sealed record DapExitedEvent(long Generation, int? ExitCode);
