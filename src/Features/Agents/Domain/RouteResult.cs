namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Narrow route result describing parse outcome and intent.
/// Explicit failure information for unknown/ambiguous/multiple/empty cases.
/// </summary>
public sealed record RouteResult(
    bool Success,
    RouteRequest? Request,
    string? FailureReason);
