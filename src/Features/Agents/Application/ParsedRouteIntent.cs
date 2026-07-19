namespace Zaide.Features.Agents.Application;

/// <summary>
/// Parse-stage routing intent before typed target resolution.
/// Visible-name matching remains an internal parse detail only.
/// </summary>
public sealed record ParsedRouteIntent(
    string SourcePanelId,
    string? MatchedAgentName,
    string ContentAfterStrip,
    bool IsDirectSend);
