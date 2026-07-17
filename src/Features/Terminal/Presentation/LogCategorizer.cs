using System;

namespace Zaide.Features.Terminal.Presentation;

/// <summary>
/// Best-effort categorizer that assigns a <see cref="LogCategory"/> and warning
/// flag to terminal output lines based on content heuristics.
///
/// Unrecognized lines default to <see cref="LogCategory.Log"/>.
/// </summary>
public static class LogCategorizer
{
    /// <summary>
    /// Categorizes a single line of terminal output.
    /// </summary>
    /// <param name="line">The raw output line (may include ANSI escape sequences).</param>
    /// <returns>A tuple of (category, hasWarning).</returns>
    public static (LogCategory Category, bool HasWarning) Categorize(string line)
    {
        if (string.IsNullOrEmpty(line))
            return (LogCategory.Log, false);

        // Strip ANSI escape sequences for heuristic matching
        string plain = StripAnsi(line);

        // Check for warning/exception indicators first
        bool hasWarning = IsWarning(plain);

        // Heuristic: explicit tag markers
        if (plain.Contains("[BUILD]", StringComparison.OrdinalIgnoreCase))
            return (LogCategory.Build, hasWarning);

        if (plain.Contains("[AGENT]", StringComparison.OrdinalIgnoreCase))
            return (LogCategory.Agent, hasWarning);

        if (plain.Contains("[LOG]", StringComparison.OrdinalIgnoreCase))
            return (LogCategory.Log, hasWarning);

        // Heuristic: build tool output
        if (IsBuildOutput(plain))
            return (LogCategory.Build, hasWarning);

        // Heuristic: agent-related keywords
        if (IsAgentOutput(plain))
            return (LogCategory.Agent, hasWarning);

        // Default: runtime log
        return (LogCategory.Log, hasWarning);
    }

    /// <summary>
    /// Returns true if the line looks like a warning or exception.
    /// </summary>
    private static bool IsWarning(string plain)
    {
        if (plain.Contains("warning", StringComparison.OrdinalIgnoreCase))
            return true;

        if (plain.Contains("exception", StringComparison.OrdinalIgnoreCase))
            return true;

        if (plain.Contains("error", StringComparison.OrdinalIgnoreCase))
            return true;

        if (plain.Contains("failed", StringComparison.OrdinalIgnoreCase))
            return true;

        if (plain.Contains("traceback", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Returns true if the line looks like build system output.
    /// </summary>
    private static bool IsBuildOutput(string plain)
    {
        // Common build tool patterns
        if (plain.StartsWith("Build", StringComparison.OrdinalIgnoreCase))
            return true;

        if (plain.StartsWith("  ", StringComparison.Ordinal) && plain.Contains('→'))
            return true;

        if (plain.Contains("error CS", StringComparison.Ordinal))
            return true;

        if (plain.Contains("warning CS", StringComparison.Ordinal))
            return true;

        if (plain.Contains("msbuild", StringComparison.OrdinalIgnoreCase))
            return true;

        if (plain.Contains("dotnet ", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Returns true if the line looks like agent-related output.
    /// </summary>
    private static bool IsAgentOutput(string plain)
    {
        if (plain.Contains("agent", StringComparison.OrdinalIgnoreCase))
            return true;

        if (plain.Contains("townhall", StringComparison.OrdinalIgnoreCase))
            return true;

        if (plain.Contains("model:", StringComparison.OrdinalIgnoreCase))
            return true;

        if (plain.Contains("prompt", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Strips ANSI escape sequences from a string for plain-text matching.
    /// </summary>
    private static string StripAnsi(string input)
    {
        // Simple ANSI escape sequence removal: \x1B[...m, \x1B[...H, etc.
        int start = input.IndexOf('\x1B');
        if (start < 0)
            return input;

        var result = new System.Text.StringBuilder(input.Length);
        int i = 0;
        while (i < input.Length)
        {
            if (input[i] == '\x1B' && i + 1 < input.Length && input[i + 1] == '[')
            {
                // Skip past the escape sequence
                i += 2; // skip \x1B[
                while (i < input.Length && input[i] >= 0x20 && input[i] <= 0x3F)
                    i++; // skip parameter bytes
                if (i < input.Length && input[i] >= 0x40 && input[i] <= 0x7E)
                    i++; // skip final byte
            }
            else
            {
                result.Append(input[i]);
                i++;
            }
        }

        return result.ToString();
    }
}