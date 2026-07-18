using System;
using System.Collections.Generic;
using System.Linq;
using Zaide.Features.Editor.Presentation;

namespace Zaide.Tests.Architecture;

/// <summary>
/// Refactor 6.1 M3 legacy-violation allowlist.
/// <para>
/// <b>Allowlist mutation rule (exact):</b>
/// </para>
/// <list type="number">
/// <item>
/// <b>Add</b> an entry only when all of the following are true:
/// (a) it maps to an existing M0 finding ID (R61-V##) or an explicitly documented
/// deferred exception already recorded in the Refactor 6.1 plan;
/// (b) the hybrid inventory already shows matching live evidence for the exact
/// <see cref="ArchitectureAllowlistEntry.MatchKey"/>;
/// (c) the same review/rollback unit updates this allowlist, the architecture
/// ratchet tests (including the frozen FindingId set), and the Refactor 6.1 plan
/// rationale; (d) the addition is not used to hide newly introduced debt without
/// human review.
/// </item>
/// <item>
/// <b>Remove</b> an entry only in the same change that eliminates the live
/// inventory evidence for its MatchKey. Removed keys must not reappear without
/// a new reviewed Add.
/// </item>
/// <item>
/// <b>Change</b> MatchKey, Category, or M0FindingId only as an explicit
/// remove+add pair in one review unit. Rationale/owner/disposition/removal-
/// boundary text may be clarified without changing MatchKey when the debt site
/// is unchanged.
/// </item>
/// <item>
/// Broad wildcard exceptions are forbidden. File-level keys are required for
/// locator sites and root admissions; namespace edges are keyed by technical
/// folder direction plus exact source path.
/// </item>
/// <item>
/// Allowlist growth requires an explicit test/doc update of the frozen
/// FindingId baseline in <see cref="ArchitectureRatchetTests"/>. Count-only
/// increases without updating that baseline fail the ratchet.
/// </item>
/// </list>
/// <para>
/// M3 scopes: NamespaceDirection, LocatorSite, RootFolderAdmission only.
/// Public-type full-name baselines and expanded technical-folder admissions are
/// M4 (<see cref="PublicProductionTypeBaseline"/>,
/// <see cref="ArchitectureVisibilityRatchet"/>). Target feature-layout
/// enforcement remains Refactor 6.2.
/// </para>
/// </summary>
public static class LegacyArchitectureAllowlist
{
    public const string DispositionDependencyInversion = "DependencyInversionLifetime";
    public const string DispositionDeferredException = "DeferredException";
    public const string DispositionMovementOnly = "MovementOnly";

    /// <summary>
    /// Frozen M3 FindingId set. Growing this set requires an intentional review
    /// that also updates architecture tests and the Refactor 6.1 plan.
    /// </summary>
    public static readonly IReadOnlyList<string> ApprovedFindingIds = new[]
    {
        "R61-AL-LOC-App",
        "R61-AL-LOC-Program",
    };

    private static readonly IReadOnlyList<ArchitectureAllowlistEntry> EntriesInternal =
        CreateEntries();

    /// <summary>Deterministic allowlist ordered by FindingId.</summary>
    public static IReadOnlyList<ArchitectureAllowlistEntry> Entries => EntriesInternal;

    public static IReadOnlyList<ArchitectureAllowlistEntry> EntriesForCategory(string category) =>
        EntriesInternal
            .Where(e => e.Category == category)
            .OrderBy(e => e.FindingId, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<ArchitectureAllowlistEntry> CreateEntries()
    {
        var entries = new List<ArchitectureAllowlistEntry>
        {
            // NamespaceDirection: empty after M5 (SourceControlState Domain→Application
            // residual deleted). Services -> ViewModels / Models -> Services remain
            // ratcheted when technical folders reappear.

            // --- LocatorSite (exact production files with provider evidence) ---
            new(
                findingId: "R61-AL-LOC-Program",
                category: ArchitectureRatchet.CategoryLocatorSite,
                matchKey: ArchitectureRatchet.BuildLocatorMatchKey("src/App/Composition/Program.cs"),
                m0FindingId: "R61-V09",
                owner: "App/Composition",
                disposition: DispositionDependencyInversion,
                rationale:
                "Program assigns static App.Services and uses the provider inside the " +
                "composition-root editor factory. Global locator debt for Refactor 6.3.",
                removalBoundary: "Refactor 6.3"),

            new(
                findingId: "R61-AL-LOC-App",
                category: ArchitectureRatchet.CategoryLocatorSite,
                matchKey: ArchitectureRatchet.BuildLocatorMatchKey("src/App/Composition/App.axaml.cs"),
                m0FindingId: "R61-V09",
                owner: "App/Composition",
                disposition: DispositionDependencyInversion,
                rationale:
                "App owns static Services, startup resolution, and DisposeServicesOnExit " +
                "provider lookups (R61-V09 / R61-V12 composition and shutdown debt).",
                removalBoundary: "Refactor 6.3"),

            // RootFolderAdmission: empty by design. Deny-by-default for tracked
            // production C# under src/Infrastructure/ and src/UI/Shared/ — any
            // live C# admission without a reviewed entry is a NEW_VIOLATION.
            // Non-C# assets are outside this inventory/ratchet.
        };

        return entries
            .OrderBy(e => e.FindingId, StringComparer.Ordinal)
            .ToArray();
    }
}
