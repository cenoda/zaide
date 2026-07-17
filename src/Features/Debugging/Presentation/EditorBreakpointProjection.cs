using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zaide.App.Composition;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Debugging.Application;

namespace Zaide.Features.Debugging.Presentation;

/// <summary>
/// Pure projection helpers for editor breakpoint margin state.
/// </summary>
internal static class EditorBreakpointProjection
{
    public static string? NormalizeDocumentPath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        return Path.GetFullPath(filePath);
    }

    public static bool HasSelectedWorkspace(string? workspaceRoot) =>
        !string.IsNullOrWhiteSpace(workspaceRoot);

    public static int GetLineCount(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return 1;

        var count = 1;
        foreach (var ch in text)
        {
            if (ch == '\n')
                count++;
        }

        return count;
    }

    public static bool IsValidCaretLine(int line, string? text) =>
        line >= 1 && line <= GetLineCount(text);

    public static IReadOnlyList<EditorBreakpointMarker> ForSource(
        IReadOnlyList<PersistedBreakpoint>? breakpoints,
        string normalizedSourcePath,
        IReadOnlyList<DebugBreakpointVerification>? verifications = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedSourcePath);

        if (breakpoints is null || breakpoints.Count == 0)
            return Array.Empty<EditorBreakpointMarker>();

        var verificationByRequestedLine = BuildVerificationLookup(
            verifications,
            normalizedSourcePath);

        return breakpoints
            .Where(bp => string.Equals(
                bp.SourcePath,
                normalizedSourcePath,
                StringComparison.Ordinal))
            .OrderBy(bp => bp.Line)
            .Select(bp =>
            {
                verificationByRequestedLine.TryGetValue(bp.Line, out var verification);
                return new EditorBreakpointMarker(
                    bp.Line,
                    bp.Enabled,
                    verification?.State,
                    verification?.Message);
            })
            .ToArray();
    }

    private static Dictionary<int, DebugBreakpointVerification> BuildVerificationLookup(
        IReadOnlyList<DebugBreakpointVerification>? verifications,
        string normalizedSourcePath)
    {
        var map = new Dictionary<int, DebugBreakpointVerification>();
        if (verifications is null || verifications.Count == 0)
            return map;

        foreach (var verification in verifications)
        {
            if (!string.Equals(
                    verification.SourcePath,
                    normalizedSourcePath,
                    StringComparison.Ordinal))
            {
                continue;
            }

            map[verification.RequestedLine] = verification;
        }

        return map;
    }
}
