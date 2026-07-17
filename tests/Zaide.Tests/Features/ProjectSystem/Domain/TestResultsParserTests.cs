using System;
using System.IO;
using Xunit;
using Zaide.App.Composition;
using Zaide.Features.ProjectSystem.Domain;

namespace Zaide.Tests.Features.ProjectSystem.Domain;

/// <summary>
/// Phase 11 M5 / F7 tests for dotnet test console parsing.
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

    [Fact]
    public void Parse_VstestPassSummary_ExtractsCountsAndCaseLine()
    {
        var lines = new[]
        {
            "Starting test execution, please wait...",
            "A total of 1 test files matched the specified pattern.",
            "  Passed WorkflowTestsPass.PassingTests.Always_passes [1 ms]",
            "Test Run Successful.",
            "Total tests: 1",
            "     Passed: 1",
            " Total time: 0.3338 Seconds",
        };

        var (cases, summary, complete) = TestResultsParser.Parse(lines, TempRoot);

        var testCase = Assert.Single(cases);
        Assert.Equal(TestCaseOutcome.Passed, testCase.Outcome);
        Assert.True(complete);
        Assert.Equal(1, summary!.Passed);
        Assert.Equal(1, summary.Total);
    }

    [Fact]
    public void Parse_VstestFailSummary_ExtractsFailedCaseAndCounts()
    {
        var file = Path.Combine(TempRoot, "FailingTests.cs");
        var lines = new[]
        {
            "[xUnit.net 00:00:00.08]     WorkflowTestsFail.FailingTests.Intentionally_fails [FAIL]",
            "  Failed WorkflowTestsFail.FailingTests.Intentionally_fails [3 ms]",
            "  Error Message:",
            "   Assert.Equal() Failure: Values differ",
            "  Stack Trace:",
            $"     at WorkflowTestsFail.FailingTests.Intentionally_fails() in {file}:line 6",
            "Test Run Failed.",
            "Total tests: 1",
            "     Failed: 1",
            " Total time: 0.3353 Seconds",
        };

        var (cases, summary, complete) = TestResultsParser.Parse(lines, TempRoot);

        var testCase = Assert.Single(cases);
        Assert.Equal(TestCaseOutcome.Failed, testCase.Outcome);
        Assert.Equal(Path.GetFullPath(file), testCase.FilePath);
        Assert.True(complete);
        Assert.Equal(1, summary!.Failed);
    }

    [Fact]
    public void Parse_XunitStackFramePathLineCol_ResolvesNavigation()
    {
        var file = Path.Combine(TempRoot, "FailingTests.cs");
        var lines = new[]
        {
            "[xUnit.net 00:00:00.08]     WorkflowTestsFail.FailingTests.Intentionally_fails [FAIL]",
            $"[xUnit.net 00:00:00.08]         {file}(6,0): at WorkflowTestsFail.FailingTests.Intentionally_fails()",
            "  Failed WorkflowTestsFail.FailingTests.Intentionally_fails [3 ms]",
            "Failed!  - Failed:     1, Passed:     0, Skipped:     0, Total:     1, Duration: 10 ms",
        };

        var (cases, summary, complete) = TestResultsParser.Parse(lines, TempRoot);

        var testCase = Assert.Single(cases);
        Assert.Equal(Path.GetFullPath(file), testCase.FilePath);
        Assert.Equal(6, testCase.Line);
        Assert.True(complete);
        Assert.Equal(1, summary!.Failed);
    }

    [Fact]
    public void Parse_FailSummaryWithoutCaseDetails_ReportsSummaryOnly()
    {
        var lines = new[]
        {
            "Starting test execution, please wait...",
            "Failed!  - Failed:     2, Passed:     0, Skipped:     0, Total:     2, Duration: 10 ms",
        };

        var (cases, summary, complete) = TestResultsParser.Parse(lines, TempRoot);

        Assert.Empty(cases);
        Assert.NotNull(summary);
        Assert.Equal(2, summary!.Failed);
        Assert.True(complete);
    }

    [Fact]
    public void Parse_TestRunSuccessfulWithoutCounts_IsNotComplete()
    {
        var lines = new[]
        {
            "Test Run Successful.",
            " Total time: 0.3338 Seconds",
        };

        var (cases, summary, complete) = TestResultsParser.Parse(lines, TempRoot);

        Assert.Empty(cases);
        Assert.Null(summary);
        Assert.False(complete);
    }

    [Fact]
    public void Parse_MalformedTruncatedBanner_FailOpen()
    {
        var lines = new[]
        {
            "Passed!  - Failed:     0, Passed:",
            "Test Run Successful.",
        };

        var (cases, summary, complete) = TestResultsParser.Parse(lines, TempRoot);

        Assert.Empty(cases);
        Assert.Null(summary);
        Assert.False(complete);
    }

    [Fact]
    public void Parse_FailedCaseWithoutNavigableStack_StillParsesCase()
    {
        var lines = new[]
        {
            "  Failed WorkflowTestsFail.FailingTests.Intentionally_fails [3 ms]",
            "  Error Message:",
            "   boom",
            "  Stack Trace:",
            "     at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)",
            "Failed!  - Failed:     1, Passed:     0, Skipped:     0, Total:     1, Duration: 10 ms",
        };

        var (cases, summary, complete) = TestResultsParser.Parse(lines, TempRoot);

        var testCase = Assert.Single(cases);
        Assert.Equal(TestCaseOutcome.Failed, testCase.Outcome);
        Assert.Null(testCase.FilePath);
        Assert.Null(testCase.Line);
        Assert.NotNull(testCase.StackTrace);
        Assert.True(complete);
        Assert.Equal(1, summary!.Failed);
    }
}