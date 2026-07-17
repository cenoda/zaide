using System.Collections.Generic;

namespace Zaide.Features.ProjectSystem.Domain;

/// <summary>
/// Immutable snapshot of structured test results for one test generation.
/// </summary>
public sealed record TestResultsSnapshot(
    long Generation,
    ProjectWorkflowOutcomeKind? OperationOutcome,
    bool IsPartial,
    TestResultsSummary? Summary,
    IReadOnlyList<TestCaseResult> Cases)
{
    public static TestResultsSnapshot Empty { get; } = new(
        Generation: 0,
        OperationOutcome: null,
        IsPartial: false,
        Summary: null,
        Cases: System.Array.Empty<TestCaseResult>());
}
