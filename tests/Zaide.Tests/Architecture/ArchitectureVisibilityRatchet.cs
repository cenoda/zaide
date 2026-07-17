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
    /// Top-level <c>src/</c> folders admitted for tracked production C#.
    /// Includes remaining technical-layer folders, Refactor 6.2 M1
    /// <c>UI</c> (only <c>src/UI/DesignSystem/</c>), and Refactor 6.2 M2–M4
    /// <c>Features</c> (only <c>src/Features/Settings/</c>,
    /// <c>src/Features/Workspace/</c>, <c>src/Features/Editor/</c>, and
    /// <c>src/Features/ProjectSystem/</c>; see
    /// <see cref="IsApprovedFeaturesPath"/>). Other feature-first roots remain
    /// deny-by-default until their migration slices update this set.
    /// </summary>
    public static readonly IReadOnlyList<string> ApprovedCurrentTechnicalFolders = new[]
    {
        "Features",
        "Models",
        "Services",
        "UI",
        "ViewModels",
        "Views",
    };

    /// <summary>
    /// Refactor 6.2 M1: only DesignSystem is admitted under <c>src/UI/</c>.
    /// <c>src/UI/Shared/</c> remains deny-by-default (also checked above).
    /// </summary>
    public static bool IsApprovedUiPath(string relativePath)
    {
        var path = relativePath.Replace('\\', '/').Trim();
        return path.StartsWith("src/UI/DesignSystem/", StringComparison.Ordinal);
    }

    /// <summary>
    /// Refactor 6.2 M2–M5a: Settings, Workspace, Editor, and ProjectSystem are
    /// admitted under <c>src/Features/</c>. Other features remain deny-by-default
    /// until their migration slices.
    /// </summary>
    public static bool IsApprovedFeaturesPath(string relativePath)
    {
        var path = relativePath.Replace('\\', '/').Trim();
        return path.StartsWith("src/Features/Settings/", StringComparison.Ordinal)
            || path.StartsWith("src/Features/Workspace/", StringComparison.Ordinal)
            || path.StartsWith("src/Features/Editor/", StringComparison.Ordinal)
            || path.StartsWith("src/Features/ProjectSystem/", StringComparison.Ordinal);
    }

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

            if (source.TechnicalFolder == "UI")
            {
                if (!IsApprovedUiPath(path))
                {
                    violations.Add(new ArchitectureViolation(
                        ArchitectureRatchet.CategoryRootFolderAdmission,
                        ArchitectureRatchet.BuildRootAdmissionMatchKey(path),
                        path,
                        "unauthorized path under src/UI/; only src/UI/DesignSystem/ " +
                        "is admitted (Refactor 6.2 M1). UI/Shared remains deny-by-default."));
                }

                continue;
            }

            if (source.TechnicalFolder == "Features")
            {
                if (!IsApprovedFeaturesPath(path))
                {
                    violations.Add(new ArchitectureViolation(
                        ArchitectureRatchet.CategoryRootFolderAdmission,
                        ArchitectureRatchet.BuildRootAdmissionMatchKey(path),
                        path,
                        "unauthorized path under src/Features/; only src/Features/Settings/, " +
                        "src/Features/Workspace/, src/Features/Editor/, and " +
                        "src/Features/ProjectSystem/ are admitted " +
                        "(Refactor 6.2 M2–M5a). " +
                        "Other features require their slice."));
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
                    "approved folders: Features (Settings + Workspace), Models, Services, " +
                    "UI (DesignSystem only), ViewModels, Views. Other feature-first " +
                    "folders require a Refactor 6.2 migration slice."));
            }
        }

        return violations
            .OrderBy(v => v.MatchKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizePath(string relativePath) =>
        relativePath.Replace('\\', '/').Trim();
}
