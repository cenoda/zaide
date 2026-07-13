using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Zaide.Services;

/// <summary>
/// Parses common MSBuild / Roslyn CLI diagnostic lines from build output.
/// Unparseable lines are ignored.
/// </summary>
public static partial class BuildDiagnosticParser
{
    // path(line,col): error CSxxxx: message [optional project suffix]
    // path(line): warning CSxxxx: message
    [GeneratedRegex(
        @"^(?<path>.+?)\((?<line>\d+)(?:,(?<col>\d+))?\):\s*(?<severity>error|warning)\s+(?:(?<code>\S+):\s*)?(?<message>.+?)(?:\s+\[[^\]]+\])?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    private static partial Regex DiagnosticLineRegex();

    /// <summary>
    /// Parses diagnostic lines from build output.
    /// Relative paths resolve against <paramref name="workingDirectory"/>.
    /// </summary>
    public static IReadOnlyList<BuildDiagnostic> Parse(
        IEnumerable<string> lines,
        string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        var normalizedCwd = Path.GetFullPath(workingDirectory);
        var seen = new HashSet<BuildDiagnosticKey>();
        var results = new List<BuildDiagnostic>();

        foreach (var rawLine in lines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
                continue;

            var match = DiagnosticLineRegex().Match(rawLine.Trim());
            if (!match.Success)
                continue;

            if (!int.TryParse(match.Groups["line"].Value, out var line) || line < 1)
                continue;

            var column = 1;
            if (match.Groups["col"].Success &&
                int.TryParse(match.Groups["col"].Value, out var parsedCol) &&
                parsedCol >= 1)
            {
                column = parsedCol;
            }

            var severityText = match.Groups["severity"].Value;
            var severity = severityText.Equals("warning", StringComparison.OrdinalIgnoreCase)
                ? LanguageDiagnosticSeverity.Warning
                : LanguageDiagnosticSeverity.Error;

            var code = match.Groups["code"].Success && match.Groups["code"].Length > 0
                ? match.Groups["code"].Value
                : null;

            var message = match.Groups["message"].Value.Trim();
            if (message.Length == 0)
                continue;

            var filePath = NormalizePath(match.Groups["path"].Value.Trim(), normalizedCwd);
            if (filePath is null)
                continue;

            var diagnostic = new BuildDiagnostic(
                filePath,
                line,
                column,
                severity,
                code,
                message);

            var key = new BuildDiagnosticKey(diagnostic);
            if (!seen.Add(key))
                continue;

            results.Add(diagnostic);
        }

        results.Sort(static (a, b) =>
        {
            var pathCompare = string.Compare(a.FilePath, b.FilePath, StringComparison.Ordinal);
            if (pathCompare != 0)
                return pathCompare;

            var lineCompare = a.Line.CompareTo(b.Line);
            if (lineCompare != 0)
                return lineCompare;

            var colCompare = a.Column.CompareTo(b.Column);
            if (colCompare != 0)
                return colCompare;

            var severityCompare = a.Severity.CompareTo(b.Severity);
            if (severityCompare != 0)
                return severityCompare;

            return string.Compare(a.Message, b.Message, StringComparison.Ordinal);
        });

        return results;
    }

    private static string? NormalizePath(string rawPath, string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
            return null;

        try
        {
            var combined = Path.IsPathRooted(rawPath)
                ? rawPath
                : Path.Combine(workingDirectory, rawPath);
            return Path.GetFullPath(combined);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private readonly struct BuildDiagnosticKey : IEquatable<BuildDiagnosticKey>
    {
        private readonly string _filePath;
        private readonly int _line;
        private readonly int _column;
        private readonly LanguageDiagnosticSeverity _severity;
        private readonly string? _code;
        private readonly string _message;

        public BuildDiagnosticKey(BuildDiagnostic diagnostic)
        {
            _filePath = diagnostic.FilePath;
            _line = diagnostic.Line;
            _column = diagnostic.Column;
            _severity = diagnostic.Severity;
            _code = diagnostic.Code;
            _message = diagnostic.Message;
        }

        public bool Equals(BuildDiagnosticKey other) =>
            _line == other._line &&
            _column == other._column &&
            _severity == other._severity &&
            string.Equals(_filePath, other._filePath, StringComparison.Ordinal) &&
            string.Equals(_code, other._code, StringComparison.Ordinal) &&
            string.Equals(_message, other._message, StringComparison.Ordinal);

        public override bool Equals(object? obj) =>
            obj is BuildDiagnosticKey other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(_filePath),
                _line,
                _column,
                _severity,
                _code,
                _message);
    }
}
