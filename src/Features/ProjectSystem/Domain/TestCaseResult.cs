namespace Zaide.Features.ProjectSystem.Domain;

/// <summary>
/// Immutable parsed result for one test case.
/// </summary>
public sealed record TestCaseResult(
    string? FullyQualifiedName,
    string DisplayName,
    TestCaseOutcome Outcome,
    string? Duration,
    string? ErrorMessage,
    string? StackTrace,
    string? FilePath,
    int? Line);
