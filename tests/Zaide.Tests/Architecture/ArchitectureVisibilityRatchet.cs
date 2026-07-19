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
    /// Refactor 6.2 M12 completed the feature-first tree: <c>App</c>
    /// (Composition + Shell only; see <see cref="IsApprovedAppPath"/>),
    /// <c>UI</c> (DesignSystem only), and <c>Features</c> (all migrated
    /// features; see <see cref="IsApprovedFeaturesPath"/>). Technical-layer
    /// folders and root composition files are no longer admitted.
    /// </summary>
    public static readonly IReadOnlyList<string> ApprovedCurrentTechnicalFolders = new[]
    {
        "App",
        "Features",
        "UI",
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
    /// Refactor 6.2 M2–M11 + Refactor 7 M1 Conversations: Settings, Workspace,
    /// Editor, ProjectSystem, Language (including Infrastructure/Lsp), Debugging,
    /// SourceControl, Terminal, Townhall, Agents, and Conversations are admitted
    /// under <c>src/Features/</c>.
    /// </summary>
    public static bool IsApprovedFeaturesPath(string relativePath)
    {
        var path = relativePath.Replace('\\', '/').Trim();
        return path.StartsWith("src/Features/Settings/", StringComparison.Ordinal)
            || path.StartsWith("src/Features/Workspace/", StringComparison.Ordinal)
            || path.StartsWith("src/Features/Editor/", StringComparison.Ordinal)
            || path.StartsWith("src/Features/ProjectSystem/", StringComparison.Ordinal)
            || path.StartsWith("src/Features/Language/", StringComparison.Ordinal)
            || path.StartsWith("src/Features/Debugging/", StringComparison.Ordinal)
            || path.StartsWith("src/Features/SourceControl/", StringComparison.Ordinal)
            || path.StartsWith("src/Features/Terminal/", StringComparison.Ordinal)
            || path.StartsWith("src/Features/Townhall/", StringComparison.Ordinal)
            || path.StartsWith("src/Features/Agents/", StringComparison.Ordinal)
            || path.StartsWith("src/Features/Conversations/", StringComparison.Ordinal);
    }

    /// <summary>
    /// Exact composition C# files permitted directly under <c>src/</c>.
    /// Empty after Refactor 6.2 M12 rehomed Program/App/MainWindow under
    /// <c>src/App/</c>. New root C# files fail admission.
    /// </summary>
    public static readonly IReadOnlyList<string> ApprovedSrcRootCompositionFiles = Array.Empty<string>();

    /// <summary>
    /// Refactor 6.2 M12: only Composition and Shell under <c>src/App/</c>.
    /// </summary>
    public static bool IsApprovedAppPath(string relativePath)
    {
        var path = relativePath.Replace('\\', '/').Trim();
        return path.StartsWith("src/App/Composition/", StringComparison.Ordinal)
            || path.StartsWith("src/App/Shell/", StringComparison.Ordinal);
    }

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
                        "no root composition C# is admitted after Refactor 6.2 M12"));
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
                        "src/Features/Workspace/, src/Features/Editor/, " +
                        "src/Features/ProjectSystem/, src/Features/Language/, " +
                        "src/Features/Debugging/, src/Features/SourceControl/, " +
                        "src/Features/Terminal/, src/Features/Townhall/, src/Features/Agents/, " +
                        "and src/Features/Conversations/ are admitted (Refactor 6.2 M2–M11; " +
                        "Refactor 7 M1 Conversations). " +
                        "Other features require their slice."));
                }

                continue;
            }

            if (source.TechnicalFolder == "App")
            {
                if (!IsApprovedAppPath(path))
                {
                    violations.Add(new ArchitectureViolation(
                        ArchitectureRatchet.CategoryRootFolderAdmission,
                        ArchitectureRatchet.BuildRootAdmissionMatchKey(path),
                        path,
                        "unauthorized path under src/App/; only src/App/Composition/ and " +
                        "src/App/Shell/ are admitted (Refactor 6.2 M12)."));
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
                    "approved folders: App (Composition + Shell), Features (all migrated), " +
                    "UI (DesignSystem only). Technical-layer folders are no longer admitted " +
                    "after Refactor 6.2 M12."));
            }
        }

        return violations
            .OrderBy(v => v.MatchKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizePath(string relativePath) =>
        relativePath.Replace('\\', '/').Trim();
}
