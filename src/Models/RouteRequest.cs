namespace Zaide.Models;

/// <summary>
/// Narrow route request describing parsed user input for direct or routed send.
/// M2 only — zero or one explicit mention target supported.
/// </summary>
public sealed record RouteRequest(
    string SourcePanelId,
    string? TargetAgentName,
    string ContentAfterStrip,
    bool IsDirectSend);
