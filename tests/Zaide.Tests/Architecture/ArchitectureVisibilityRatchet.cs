using System;
using System.Collections.Generic;
using System.Linq;

namespace Zaide.Tests.Architecture;

/// <summary>
/// Refactor 6.1 M4: public/internal visibility ratchet and expanded root-folder
/// admission helpers. Complements <see cref="ArchitectureRatchet"/> without
/// weakening M3 legacy allowlist behavior.
/// </summary>
public static class ArchitectureVisibilityRatchet
{
    public const string FailureNewPublicType = "NEW_PUBLIC_TYPE";
    public const string FailureStalePublicBaseline = "STALE_PUBLIC_BASELINE";
    public const string FailureVisibilityBaselineIntegrity = "VISIBILITY_BASELINE_INTEGRITY";

    /// <summary>
    /// Technical folders allowed by the still-current tree (M0 inventory / M1
    /// conventions). Any other top-level folder under <c>src/</c> is deny-by-default.
    /// Feature-first layout enforcement is Refactor 6.2, not M4.
    /// </summary>
    public static readonly IReadOnlyList<string> ApprovedCurrentTechnicalFolders = new[]
    {
        "Models",
        "Services",
        "Styles",
        "ViewModels",
        "Views",
    };

    /// <summary>
    /// Exact composition C# files permitted directly under <c>src/</c> (M0: three
    /// root files). New root C# files fail admission unless the baseline is
    /// intentionally updated in the same review.
    /// </summary>
    public static readonly IReadOnlyList<string> ApprovedSrcRootCompositionFiles = new[]
    {
        "src/App.axaml.cs",
        "src/MainWindow.axaml.cs",
        "src/Program.cs",
    };

    public static IReadOnlyList<string> GetLivePublicFullNames(ArchitectureInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);

        return inventory.Types
            .Where(t => t.IsPublic)
            .Select(t => t.FullName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<string> FindNewPublicTypes(
        IReadOnlyList<string> livePublicFullNames,
        IReadOnlyList<string> approvedPublicFullNames)
    {
        ArgumentNullException.ThrowIfNull(livePublicFullNames);
        ArgumentNullException.ThrowIfNull(approvedPublicFullNames);

        var approved = new HashSet<string>(approvedPublicFullNames, StringComparer.Ordinal);
        return livePublicFullNames
            .Where(n => !approved.Contains(n))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<string> FindStalePublicBaselineNames(
        IReadOnlyList<string> livePublicFullNames,
        IReadOnlyList<string> approvedPublicFullNames)
    {
        ArgumentNullException.ThrowIfNull(livePublicFullNames);
        ArgumentNullException.ThrowIfNull(approvedPublicFullNames);

        var live = new HashSet<string>(livePublicFullNames, StringComparer.Ordinal);
        return approvedPublicFullNames
            .Where(n => !live.Contains(n))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
    }

    public static string FormatNewPublicTypes(IReadOnlyList<string> fullNames) =>
        string.Join(
            Environment.NewLine,
            fullNames.Select(n =>
                $"{FailureNewPublicType}: {n}. " +
                "Default to internal; if public is required, update " +
                $"{PublicProductionTypeBaseline.RelativeBaselinePath} in the same reviewed change."));

    public static string FormatStalePublicBaseline(IReadOnlyList<string> fullNames) =>
        string.Join(
            Environment.NewLine,
            fullNames.Select(n =>
                $"{FailureStalePublicBaseline}: {n}. " +
                "Remove the name from the baseline in the same change that removed " +
                "or internalized the type."));

    /// <summary>
    /// Expanded root-folder admissions for <b>tracked production C# only</b>
    /// (same inventory as M2/M3: <c>git ls-files</c> of <c>src/**/*.cs</c>).
    /// Beyond M3 Infrastructure/UI.Shared C# deny-by-default: unauthorized
    /// technical folders and unauthorized <c>src/</c> root composition C# files.
    /// Non-C# assets (AXAML, project files, manifests, etc.) are not detected.
    /// Does not enforce target feature-first layout (Refactor 6.2).
    /// </summary>
    public static IReadOnlyList<ArchitectureViolation> DetectExpandedRootFolderAdmissionViolations(
        ArchitectureInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);

        var allowedFolders = new HashSet<string>(ApprovedCurrentTechnicalFolders, StringComparer.Ordinal);
        var allowedRootFiles = new HashSet<string>(ApprovedSrcRootCompositionFiles, StringComparer.Ordinal);
        var violations = new List<ArchitectureViolation>();

        foreach (var source in inventory.SourceFiles)
        {
            var path = NormalizePath(source.RelativePath);

            // M3 paths (also reported by ArchitectureRatchet) — keep keys stable.
            if (path.StartsWith("src/Infrastructure/", StringComparison.Ordinal))
            {
                violations.Add(new ArchitectureViolation(
                    ArchitectureRatchet.CategoryRootFolderAdmission,
                    ArchitectureRatchet.BuildRootAdmissionMatchKey(path),
                    path,
                    "under src/Infrastructure/ (deny-by-default multi-feature root)"));
                continue;
            }

            if (path.StartsWith("src/UI/Shared/", StringComparison.Ordinal))
            {
                violations.Add(new ArchitectureViolation(
                    ArchitectureRatchet.CategoryRootFolderAdmission,
                    ArchitectureRatchet.BuildRootAdmissionMatchKey(path),
                    path,
                    "under src/UI/Shared/ (deny-by-default shared UI root)"));
                continue;
            }

            if (source.TechnicalFolder == "src")
            {
                if (!allowedRootFiles.Contains(path))
                {
                    violations.Add(new ArchitectureViolation(
                        ArchitectureRatchet.CategoryRootFolderAdmission,
                        ArchitectureRatchet.BuildRootAdmissionMatchKey(path),
                        path,
                        "unauthorized src/ root composition file; " +
                        "approved set is Program.cs, App.axaml.cs, MainWindow.axaml.cs"));
                }

                continue;
            }

            if (!allowedFolders.Contains(source.TechnicalFolder))
            {
                violations.Add(new ArchitectureViolation(
                    ArchitectureRatchet.CategoryRootFolderAdmission,
                    ArchitectureRatchet.BuildRootAdmissionMatchKey(path),
                    path,
                    $"unauthorized technical folder '{source.TechnicalFolder}'; " +
                    "approved current tree folders: Models, Services, Styles, ViewModels, Views. " +
                    "Feature-first folders are Refactor 6.2 movement, not silent admission."));
            }
        }

        return violations
            .OrderBy(v => v.MatchKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizePath(string relativePath) =>
        relativePath.Replace('\\', '/').Trim();
}
