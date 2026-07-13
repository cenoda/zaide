using System;
using System.IO;
using Xunit;
using Zaide.Services;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 11 M5 tests for dotnet test console parsing.
/// </summary>
public sealed class TestResultsParserTests
{
    private static readonly string TempRoot = Path.Combine(
        Path.GetTempPath(),
        "zaide-phase11-test-parser-" + Guid.NewGuid().ToString("N"));

    static TestResultsParserTests()
    {
        Directory.CreateDirectory(TempRoot);
    }

    [Fact]
    public void Parse_PassSummary_ExtractsCountsWithoutInventingCases()
    {
        var lines = new[]
        {
            "Starting test execution, please wait...",
            "Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: 6 ms - WorkflowTestsPass.dll (net10.0)",
        };

        var (cases, summary, complete) = TestResultsParser.Parse(lines, TempRoot);

        Assert.Empty(cases);
        Assert.True(complete);
        Assert.NotNull(summary);
        Assert.Equal(1, summary!.Passed);
        Assert.Equal(0, summary.Failed);
        Assert.Equal(1, summary.Total);
    }

    [Fact]
    public void Parse_FailOutput_ExtractsFailedCaseAndSummary()
    {
        var file = Path.Combine(TempRoot, "FailingTests.cs");
        var lines = new[]
        {
            "[xUnit.net 00:00:00.09]     WorkflowTestsFail.FailingTests.Intentionally_fails [FAIL]",
            "  Failed WorkflowTestsFail.FailingTests.Intentionally_fails [3 ms]",
            "  Error Message:",
            "   Assert.Equal() Failure: Values differ",
            "Expected: 1",
            "Actual:   2",
            "  Stack Trace:",
            $"     at WorkflowTestsFail.FailingTests.Intentionally_fails() in {file}:line 6",
            "Failed!  - Failed:     1, Passed:     0, Skipped:     0, Total:     1, Duration: 11 ms - WorkflowTestsFail.dll (net10.0)",
        };

        var (cases, summary, complete) = TestResultsParser.Parse(lines, TempRoot);

        var testCase = Assert.Single(cases);
        Assert.Equal(TestCaseOutcome.Failed, testCase.Outcome);
        Assert.Contains("Intentionally_fails", testCase.DisplayName);
        Assert.Equal(Path.GetFullPath(file), testCase.FilePath);
        Assert.Equal(6, testCase.Line);
        Assert.NotNull(testCase.ErrorMessage);
        Assert.True(complete);
        Assert.Equal(1, summary!.Failed);
        Assert.Equal(1, summary.Total);
    }

    [Fact]
    public void Parse_JunkLines_FailOpenWithoutInventedPasses()
    {
        var lines = new[]
        {
            "random build noise",
            "VSTest version 18.0.2-dev (x64)",
        };

        var (cases, summary, complete) = TestResultsParser.Parse(lines, TempRoot);

        Assert.Empty(cases);
        Assert.Null(summary);
        Assert.False(complete);
    }

    [Fact]
    public void Parse_PassedCaseLine_AddsStructuredCase()
    {
        var lines = new[]
        {
            "  Passed WorkflowTestsPass.PassingTests.Always_passes [2 ms]",
            "Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: 4 ms",
        };

        var (cases, summary, _) = TestResultsParser.Parse(lines, TempRoot);

        var testCase = Assert.Single(cases);
        Assert.Equal(TestCaseOutcome.Passed, testCase.Outcome);
        Assert.Equal(1, summary!.Passed);
    }
}
