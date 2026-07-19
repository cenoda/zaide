namespace Zaide.Features.Agents.Application;

/// <summary>
/// Parse outcome from <see cref="MentionParser"/> before typed target resolution.
/// </summary>
public sealed record MentionParseResult(
    bool Success,
    ParsedRouteIntent? Intent,
    string? FailureReason);
