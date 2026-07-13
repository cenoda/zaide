namespace Zaide.Services;

/// <summary>
/// Aggregate counts parsed from dotnet test console output when available.
/// </summary>
public sealed record TestResultsSummary(
    int? Passed,
    int? Failed,
    int? Skipped,
    int? Total);
