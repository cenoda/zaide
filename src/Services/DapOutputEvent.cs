namespace Zaide.Services;

/// <summary>
/// Adapter <c>output</c> DAP event payload projected for session consumers.
/// </summary>
/// <param name="Generation">Session generation that received the event.</param>
/// <param name="Category">Output category when present.</param>
/// <param name="Output">Output text.</param>
public sealed record DapOutputEvent(long Generation, string? Category, string Output);
