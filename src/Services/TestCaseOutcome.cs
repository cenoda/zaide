namespace Zaide.Services;

/// <summary>
/// Structured outcome for a single test case parsed from dotnet test output.
/// </summary>
public enum TestCaseOutcome
{
    Passed,
    Failed,
    Skipped,
    NotRun,
    Unknown,
}
