using System;
using System.Collections.Generic;
using System.Linq;

namespace Zaide.Tests.Architecture;

/// <summary>
/// Derives M3-ratcheted violations from the hybrid inventory and compares them
/// to the legacy allowlist. Public full-name and expanded root-folder rules live
/// in <see cref="ArchitectureVisibilityRatchet"/> (M4). Target feature-layout
/// enforcement remains Refactor 6.2.
/// </summary>
public static class ArchitectureRatchet
{
    public const string CategoryNamespaceDirection = "NamespaceDirection";
    public const string CategoryLocatorSite = "LocatorSite";
    public const string CategoryRootFolderAdmission = "RootFolderAdmission";

    public const string FailureNewViolation = "NEW_VIOLATION";
    public const string FailureStaleAllowlist = "STALE_ALLOWLIST";
    public const string FailureInventory = "INVENTORY_FAILURE";
    public const string FailureAllowlistIntegrity = "ALLOWLIST_INTEGRITY";

    /// <summary>
    /// File-level Services→ViewModels and Models→Services edges from inventory evidence.
    /// Line numbers are intentionally excluded so renumbers do not thrash the allowlist.
    /// </summary>
    public static IReadOnlyList<ArchitectureViolation> DetectNamespaceDirectionViolations(
        ArchitectureInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);

        return inventory.NamespaceDependencyEvidence
            .GroupBy(
                e => BuildNamespaceMatchKey(e.SourceTechnicalFolder, e.TargetNamespaceFragment, e.RelativePath),
                StringComparer.Ordinal)
            .Select(g =>
            {
                var sample = g.First();
                return new ArchitectureViolation(
                    CategoryNamespaceDirection,
                    g.Key,
                    sample.RelativePath,
                    $"{sample.SourceTechnicalFolder} -> {sample.TargetNamespaceFragment} " +
                    $"({g.Count()} source match(es))");
            })
            .OrderBy(v => v.MatchKey, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Exact production files that contain any provider/locator evidence kinds
    /// inventoried by M2/M7 (IServiceProvider, App.Services, CompositionRoot.Services,
    /// GetRequiredService, GetService). CompositionRoot.cs property declaration is
    /// excluded from inventory (store-only; consumers use CompositionRoot.Services).
    /// </summary>
    public static IReadOnlyList<ArchitectureViolation> DetectLocatorSiteViolations(
        ArchitectureInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);

        return inventory.ProviderEvidence
            .GroupBy(e => e.RelativePath, StringComparer.Ordinal)
            .Select(g =>
            {
                var kinds = g.Select(e => e.Kind).Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal);
                return new ArchitectureViolation(
                    CategoryLocatorSite,
                    BuildLocatorMatchKey(g.Key),
                    g.Key,
                    $"provider evidence kinds: {string.Join(", ", kinds)}; sites={g.Count()}");
            })
            .OrderBy(v => v.MatchKey, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Tracked production <b>C#</b> files under deny-by-default root folders
    /// <c>src/Infrastructure/</c> or <c>src/UI/Shared/</c> (M3 baseline).
    /// Inventory is <c>git ls-files</c> of <c>src/**/*.cs</c> only; non-C# assets
    /// are out of scope. Expanded technical-folder and src-root composition
    /// admissions are in
    /// <see cref="ArchitectureVisibilityRatchet.DetectExpandedRootFolderAdmissionViolations"/>.
    /// </summary>
    public static IReadOnlyList<ArchitectureViolation> DetectRootFolderAdmissionViolations(
        ArchitectureInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);

        return inventory.RootFolderAdmissionEvidence
            .Where(e => e.IsUnderRootInfrastructure || e.IsUnderUiShared)
            .Select(e => new ArchitectureViolation(
                CategoryRootFolderAdmission,
                BuildRootAdmissionMatchKey(e.RelativePath),
                e.RelativePath,
                e.IsUnderRootInfrastructure
                    ? "under src/Infrastructure/"
                    : "under src/UI/Shared/"))
            .OrderBy(v => v.MatchKey, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<ArchitectureViolation> DetectAllRatchetedViolations(
        ArchitectureInventory inventory)
    {
        return DetectNamespaceDirectionViolations(inventory)
            .Concat(DetectLocatorSiteViolations(inventory))
            .Concat(DetectRootFolderAdmissionViolations(inventory))
            .OrderBy(v => v.Category, StringComparer.Ordinal)
            .ThenBy(v => v.MatchKey, StringComparer.Ordinal)
            .ToArray();
    }

    public static string BuildNamespaceMatchKey(
        string sourceTechnicalFolder,
        string targetNamespaceFragment,
        string relativePath) =>
        $"namespace:{sourceTechnicalFolder}->{targetNamespaceFragment}:{NormalizePath(relativePath)}";

    public static string BuildLocatorMatchKey(string relativePath) =>
        $"locator:{NormalizePath(relativePath)}";

    public static string BuildRootAdmissionMatchKey(string relativePath) =>
        $"admission:{NormalizePath(relativePath)}";

    public static IReadOnlyList<ArchitectureViolation> FindNewViolations(
        IReadOnlyList<ArchitectureViolation> liveViolations,
        IReadOnlyList<ArchitectureAllowlistEntry> allowlist)
    {
        var allowedKeys = new HashSet<string>(
            allowlist.Select(e => e.MatchKey),
            StringComparer.Ordinal);

        return liveViolations
            .Where(v => !allowedKeys.Contains(v.MatchKey))
            .OrderBy(v => v.MatchKey, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<ArchitectureAllowlistEntry> FindUnexercisedAllowlistEntries(
        IReadOnlyList<ArchitectureViolation> liveViolations,
        IReadOnlyList<ArchitectureAllowlistEntry> allowlist)
    {
        var liveKeys = new HashSet<string>(
            liveViolations.Select(v => v.MatchKey),
            StringComparer.Ordinal);

        return allowlist
            .Where(e => !liveKeys.Contains(e.MatchKey))
            .OrderBy(e => e.FindingId, StringComparer.Ordinal)
            .ToArray();
    }

    public static string FormatNewViolations(IReadOnlyList<ArchitectureViolation> violations) =>
        string.Join(
            Environment.NewLine,
            violations.Select(v =>
                $"{FailureNewViolation}: category={v.Category}; key={v.MatchKey}; path={v.RelativePath}; {v.Summary}"));

    public static string FormatStaleAllowlist(IReadOnlyList<ArchitectureAllowlistEntry> entries) =>
        string.Join(
            Environment.NewLine,
            entries.Select(e =>
                $"{FailureStaleAllowlist}: id={e.FindingId}; key={e.MatchKey}; m0={e.M0FindingId}. " +
                "Remove the allowlist entry in the same change that cleared the debt."));

    private static string NormalizePath(string relativePath) =>
        relativePath.Replace('\\', '/').Trim();
}
