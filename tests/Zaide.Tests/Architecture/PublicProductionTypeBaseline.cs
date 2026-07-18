using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Zaide.Tests.Architecture;

/// <summary>
/// Refactor 6.1 M4 frozen public production-type baseline.
/// <para>
/// <b>Storage:</b> one full type name per line in
/// <c>tests/Zaide.Tests/Architecture/PublicProductionTypeBaseline.txt</c>
/// (source-controlled, diff-reviewable plain text). No NuGet package.
/// </para>
/// <para>
/// <b>Mutation rule (exact):</b>
/// </para>
/// <list type="number">
/// <item>
/// <b>Add</b> a public full name only when a production type is intentionally
/// made public (or newly introduced as public) in the same reviewed change that
/// updates this baseline file, the ceiling constants if needed, and the
/// Refactor 6.1 plan rationale. Prefer <c>internal</c>; public is by exception.
/// </item>
/// <item>
/// <b>Remove</b> a full name only in the same change that removes the type or
/// makes it non-public. Leaving a stale name fails with
/// <c>STALE_PUBLIC_BASELINE</c>.
/// </item>
/// <item>
/// Do <b>not</b> regenerate or overwrite this baseline during normal test
/// execution. Tests only read and compare.
/// </item>
/// <item>
/// Count-only compliance is insufficient: the explicit full-name set must match
/// live compiled public types. Ceiling remains
/// <see cref="PublicTopLevelTypes"/> (336 after Refactor 6.3 M11a Language internalization).
/// </item>
/// </list>
/// </summary>
public static class PublicProductionTypeBaseline
{
    /// <summary>Relative path of the frozen baseline text artifact (repo root).</summary>
    public const string RelativeBaselinePath =
        "tests/Zaide.Tests/Architecture/PublicProductionTypeBaseline.txt";

    /// <summary>Total non-nested, non-compiler-generated production types (unchanged by M11a visibility-only transfer).</summary>
    public const int TotalTopLevelTypes = 415;

    /// <summary>Public top-level production type ceiling and baseline count (M11a −10 Language implementations).</summary>
    public const int PublicTopLevelTypes = 336;

    /// <summary>Internal top-level production type count (M11a +10 Language implementations).</summary>
    public const int InternalTopLevelTypes = 79;

    /// <summary>
    /// Loads the approved public full names from the repository text artifact.
    /// Throws on missing file, blank lines, duplicates, or non-sorted order so
    /// integrity failures are explicit.
    /// </summary>
    public static IReadOnlyList<string> LoadApprovedPublicFullNames(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        var fullPath = Path.Combine(
            repositoryRoot,
            RelativeBaselinePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"{ArchitectureVisibilityRatchet.FailureVisibilityBaselineIntegrity}: " +
                $"public type baseline file is missing: {RelativeBaselinePath}",
                fullPath);
        }

        var rawLines = File.ReadAllLines(fullPath);
        var names = new List<string>(PublicTopLevelTypes);

        for (var i = 0; i < rawLines.Length; i++)
        {
            var line = rawLines[i].Trim();
            if (line.Length == 0)
            {
                throw new InvalidOperationException(
                    $"{ArchitectureVisibilityRatchet.FailureVisibilityBaselineIntegrity}: " +
                    $"blank line at {RelativeBaselinePath}:{i + 1}. " +
                    "Baseline must be one non-empty full name per line.");
            }

            if (line.StartsWith('#'))
            {
                throw new InvalidOperationException(
                    $"{ArchitectureVisibilityRatchet.FailureVisibilityBaselineIntegrity}: " +
                    $"comment lines are not allowed in the frozen baseline " +
                    $"({RelativeBaselinePath}:{i + 1}). Keep the artifact pure names only.");
            }

            names.Add(line);
        }

        if (names.Count != PublicTopLevelTypes)
        {
            throw new InvalidOperationException(
                $"{ArchitectureVisibilityRatchet.FailureVisibilityBaselineIntegrity}: " +
                $"baseline file has {names.Count} names; expected {PublicTopLevelTypes}.");
        }

        if (names.Count != names.Distinct(StringComparer.Ordinal).Count())
        {
            var dupes = names
                .GroupBy(n => n, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();
            throw new InvalidOperationException(
                $"{ArchitectureVisibilityRatchet.FailureVisibilityBaselineIntegrity}: " +
                $"duplicate full names in baseline: {string.Join(", ", dupes)}");
        }

        var ordered = names.OrderBy(n => n, StringComparer.Ordinal).ToArray();
        if (!names.SequenceEqual(ordered, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"{ArchitectureVisibilityRatchet.FailureVisibilityBaselineIntegrity}: " +
                "baseline file must be sorted by ordinal full name (deterministic, reviewable diffs).");
        }

        return ordered;
    }
}
