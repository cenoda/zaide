using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Zaide.Features.ProjectSystem.Domain;

/// <summary>
/// Best-effort parser for common dotnet test / VSTest console output.
/// Fail-open: returns empty cases when lines are unparsable.
/// </summary>
public static class TestResultsParser
{
    private static readonly Regex SummaryBannerRegex = new(
        @"^(Passed|Failed)!\s+-\s+Failed:\s*(\d+),\s*Passed:\s*(\d+),\s*Skipped:\s*(\d+),\s*Total:\s*(\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CaseLineRegex = new(
        @"^\s*(Passed|Failed|Skipped)\s+(.+?)(?:\s+\[([^\]]+)\])?\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex StackFrameRegex = new(
        @"at .+ in (.+):line (\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex XunitStackFrameRegex = new(
        @"(?:^\[xUnit\.net[^\]]*\]\s+)?(.+?)\((\d+),\d+\):\s*at\s+.+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex XunitFailBannerRegex = new(
        @"^\[xUnit\.net[^\]]*\]\s+(.+?)\s+\[FAIL\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TotalTestsRegex = new(
        @"^Total tests:\s*(\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex CountLineRegex = new(
        @"^\s*(Passed|Failed|Skipped):\s*(\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// Parses console lines into structured cases and optional summary counts.
    /// Never invents passing tests when lines are missing or unparsable.
    /// </summary>
    public static (IReadOnlyList<TestCaseResult> Cases, TestResultsSummary? Summary, bool ParseComplete)
        Parse(IEnumerable<string> lines, string? workingDirectory)
    {
        var cases = new List<TestCaseResult>();
        TestResultsSummary? summary = null;
        var sawSummary = false;
        var sawCase = false;

        int? totalFromBanner = null;
        int? passedFromBanner = null;
        int? failedFromBanner = null;
        int? skippedFromBanner = null;

        int? totalFromLines = null;
        int? passedFromLines = null;
        int? failedFromLines = null;
        int? skippedFromLines = null;

        string? pendingDisplayName = null;
        string? pendingFqn = null;
        string? pendingDuration = null;
        var pendingErrors = new List<string>();
        var pendingStack = new List<string>();
        var allLines = new List<string>();

        void FlushPendingFailedCase()
        {
            if (pendingDisplayName is null)
                return;

            string? filePath = null;
            int? line = null;
            var stackFrames = pendingStack.Count > 0
                ? pendingStack
                : allLines.Select(l => l.Trim()).Where(l => LooksLikeStackFrame(l)).ToList();
            var stackTrace = stackFrames.Count > 0
                ? string.Join(Environment.NewLine, stackFrames)
                : null;

            foreach (var frame in stackFrames)
            {
                if (TryParseStackFrame(frame, workingDirectory, out var parsedPath, out var parsedLine))
                {
                    filePath = parsedPath;
                    line = parsedLine;
                    break;
                }
            }

            cases.Add(new TestCaseResult(
                pendingFqn,
                pendingDisplayName,
                TestCaseOutcome.Failed,
                pendingDuration,
                pendingErrors.Count > 0 ? string.Join(Environment.NewLine, pendingErrors) : null,
                stackTrace,
                filePath,
                line));

            sawCase = true;
            pendingDisplayName = null;
            pendingFqn = null;
            pendingDuration = null;
            pendingErrors.Clear();
            pendingStack.Clear();
        }

        var inErrorMessage = false;
        var inStackTrace = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine?.TrimEnd() ?? string.Empty;
            if (line.Length == 0)
                continue;

            allLines.Add(line);

            if (line.StartsWith("[xUnit.net", StringComparison.Ordinal))
            {
                var failBannerMatch = XunitFailBannerRegex.Match(line);
                if (failBannerMatch.Success)
                {
                    inErrorMessage = false;
                    inStackTrace = false;

                    var name = failBannerMatch.Groups[1].Value.Trim();
                    if (pendingDisplayName is null)
                    {
                        pendingDisplayName = name;
                        pendingFqn = name;
                    }

                    continue;
                }

                if (LooksLikeStackFrame(line))
                {
                    pendingStack.Add(StripXunitPrefix(line).Trim());
                    continue;
                }

                continue;
            }

            var bannerMatch = SummaryBannerRegex.Match(line);
            if (bannerMatch.Success)
            {
                FlushPendingFailedCase();
                inErrorMessage = false;
                inStackTrace = false;

                failedFromBanner = int.Parse(bannerMatch.Groups[2].Value);
                passedFromBanner = int.Parse(bannerMatch.Groups[3].Value);
                skippedFromBanner = int.Parse(bannerMatch.Groups[4].Value);
                totalFromBanner = int.Parse(bannerMatch.Groups[5].Value);
                sawSummary = true;
                continue;
            }

            var totalMatch = TotalTestsRegex.Match(line);
            if (totalMatch.Success)
            {
                totalFromLines = int.Parse(totalMatch.Groups[1].Value);
                sawSummary = true;
                continue;
            }

            var countMatch = CountLineRegex.Match(line);
            if (countMatch.Success)
            {
                var label = countMatch.Groups[1].Value;
                var count = int.Parse(countMatch.Groups[2].Value);
                switch (label)
                {
                    case "Passed":
                        passedFromLines = count;
                        break;
                    case "Failed":
                        failedFromLines = count;
                        break;
                    case "Skipped":
                        skippedFromLines = count;
                        break;
                }

                sawSummary = true;
                continue;
            }

            if (inStackTrace)
            {
                if (line.StartsWith("  ", StringComparison.Ordinal) || LooksLikeStackFrame(line))
                {
                    pendingStack.Add(line.Trim());
                    continue;
                }

                FlushPendingFailedCase();
                inStackTrace = false;
            }

            if (inErrorMessage)
            {
                if (line.Trim().Equals("Stack Trace:", StringComparison.OrdinalIgnoreCase))
                {
                    inErrorMessage = false;
                    inStackTrace = true;
                    continue;
                }

                pendingErrors.Add(line.Trim());
                continue;
            }

            var caseMatch = CaseLineRegex.Match(line);
            if (caseMatch.Success)
            {
                var outcomeLabel = caseMatch.Groups[1].Value;
                var name = caseMatch.Groups[2].Value.Trim();
                var duration = caseMatch.Groups[3].Success ? caseMatch.Groups[3].Value : null;
                var outcome = MapOutcome(outcomeLabel);

                if (outcome == TestCaseOutcome.Failed &&
                    pendingDisplayName is not null &&
                    string.Equals(pendingDisplayName, name, StringComparison.Ordinal))
                {
                    pendingDuration = duration ?? pendingDuration;
                    continue;
                }

                FlushPendingFailedCase();
                inErrorMessage = false;
                inStackTrace = false;

                if (outcome == TestCaseOutcome.Failed)
                {
                    pendingDisplayName = name;
                    pendingFqn = name;
                    pendingDuration = duration;
                    continue;
                }

                cases.Add(new TestCaseResult(
                    name,
                    name,
                    outcome,
                    duration,
                    null,
                    null,
                    null,
                    null));
                sawCase = true;
                continue;
            }

            if (line.Trim().Equals("Error Message:", StringComparison.OrdinalIgnoreCase))
            {
                inErrorMessage = true;
                inStackTrace = false;
                continue;
            }

            if (line.Trim().Equals("Stack Trace:", StringComparison.OrdinalIgnoreCase))
            {
                inErrorMessage = false;
                inStackTrace = true;
                continue;
            }

            if (LooksLikeStackFrame(line) && pendingDisplayName is not null)
                pendingStack.Add(line.Trim());
        }

        FlushPendingFailedCase();

        if (totalFromBanner is not null || passedFromBanner is not null || failedFromBanner is not null ||
            skippedFromBanner is not null)
        {
            summary = new TestResultsSummary(passedFromBanner, failedFromBanner, skippedFromBanner, totalFromBanner);
        }
        else if (totalFromLines is not null || passedFromLines is not null || failedFromLines is not null ||
                 skippedFromLines is not null)
        {
            summary = new TestResultsSummary(passedFromLines, failedFromLines, skippedFromLines, totalFromLines);
        }

        var parseComplete = (sawSummary && HasMeaningfulSummary(
                totalFromBanner,
                passedFromBanner,
                failedFromBanner,
                skippedFromBanner,
                totalFromLines,
                passedFromLines,
                failedFromLines,
                skippedFromLines)) || sawCase;

        return (cases, summary, parseComplete);
    }

    private static bool HasMeaningfulSummary(
        int? totalFromBanner,
        int? passedFromBanner,
        int? failedFromBanner,
        int? skippedFromBanner,
        int? totalFromLines,
        int? passedFromLines,
        int? failedFromLines,
        int? skippedFromLines) =>
        totalFromBanner is not null ||
        passedFromBanner is not null ||
        failedFromBanner is not null ||
        skippedFromBanner is not null ||
        totalFromLines is not null ||
        passedFromLines is not null ||
        failedFromLines is not null ||
        skippedFromLines is not null;

    private static bool LooksLikeStackFrame(string line) =>
        StackFrameRegex.IsMatch(line) || XunitStackFrameRegex.IsMatch(line);

    private static bool TryParseStackFrame(
        string frame,
        string? workingDirectory,
        out string? filePath,
        out int? line)
    {
        filePath = null;
        line = null;

        var normalized = StripXunitPrefix(frame).Trim();

        var vstestMatch = StackFrameRegex.Match(normalized);
        if (vstestMatch.Success)
        {
            filePath = NormalizePath(vstestMatch.Groups[1].Value, workingDirectory);
            if (int.TryParse(vstestMatch.Groups[2].Value, out var parsedLine))
                line = parsedLine;
            return filePath is not null;
        }

        var xunitMatch = XunitStackFrameRegex.Match(normalized);
        if (xunitMatch.Success)
        {
            filePath = NormalizePath(xunitMatch.Groups[1].Value, workingDirectory);
            if (int.TryParse(xunitMatch.Groups[2].Value, out var parsedLine))
                line = parsedLine;
            return filePath is not null;
        }

        return false;
    }

    private static string StripXunitPrefix(string line)
    {
        if (!line.StartsWith("[xUnit.net", StringComparison.Ordinal))
            return line;

        var close = line.IndexOf(']');
        return close >= 0 && close + 1 < line.Length
            ? line[(close + 1)..].TrimStart()
            : line;
    }

    private static TestCaseOutcome MapOutcome(string label) =>
        label switch
        {
            "Passed" => TestCaseOutcome.Passed,
            "Failed" => TestCaseOutcome.Failed,
            "Skipped" => TestCaseOutcome.Skipped,
            _ => TestCaseOutcome.Unknown,
        };

    private static string NormalizePath(string path, string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        var full = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(workingDirectory ?? Environment.CurrentDirectory, path));

        return full;
    }
}