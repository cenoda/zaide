
using Zaide.Features.ProjectSystem.Domain;

namespace Zaide.Features.ProjectSystem.Presentation;

/// <summary>
/// UI projection of one <see cref="TestCaseResult"/>.
/// </summary>
public sealed class TestCaseItemViewModel
{
    public TestCaseItemViewModel(TestCaseResult result)
    {
        Result = result;
        DisplayText = FormatDisplayText(result);
    }

    public TestCaseResult Result { get; }

    public string DisplayText { get; }

    public bool CanNavigate =>
        !string.IsNullOrWhiteSpace(Result.FilePath) && Result.Line is > 0;

    private static string FormatDisplayText(TestCaseResult result)
    {
        var outcome = result.Outcome switch
        {
            TestCaseOutcome.Passed => "Passed",
            TestCaseOutcome.Failed => "Failed",
            TestCaseOutcome.Skipped => "Skipped",
            TestCaseOutcome.NotRun => "Not run",
            _ => "Unknown",
        };

        var duration = string.IsNullOrWhiteSpace(result.Duration) ? string.Empty : $" [{result.Duration}]";
        return $"{outcome}: {result.DisplayName}{duration}";
    }
}
