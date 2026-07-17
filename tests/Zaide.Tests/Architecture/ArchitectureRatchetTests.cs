using System;
using System.Linq;
using Xunit;
using Zaide.Features.Editor.Presentation;

namespace Zaide.Tests.Architecture;

/// <summary>
/// Refactor 6.1 M3: legacy-violation allowlist and no-new-violation ratchet.
/// Distinguishes known accepted debt, newly introduced violations, and inventory
/// / allowlist integrity failures. Public full-name and expanded root-folder
/// admissions are covered by <see cref="ArchitectureVisibilityTests"/> (M4).
/// Target feature-layout enforcement remains Refactor 6.2.
/// </summary>
public sealed class ArchitectureRatchetTests
{
    [Fact]
    public void Allowlist_IsDeterministicReadableAndWellFormed()
    {
        var entries = LegacyArchitectureAllowlist.Entries;

        Assert.NotEmpty(entries);

        // Deterministic order by FindingId.
        Assert.Equal(
            entries.OrderBy(e => e.FindingId, StringComparer.Ordinal).Select(e => e.FindingId),
            entries.Select(e => e.FindingId));

        // Unique FindingIds and MatchKeys.
        Assert.Equal(entries.Count, entries.Select(e => e.FindingId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(entries.Count, entries.Select(e => e.MatchKey).Distinct(StringComparer.Ordinal).Count());

        var allowedCategories = new[]
        {
            ArchitectureRatchet.CategoryNamespaceDirection,
            ArchitectureRatchet.CategoryLocatorSite,
            ArchitectureRatchet.CategoryRootFolderAdmission
        };

        Assert.All(entries, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.FindingId));
            Assert.False(string.IsNullOrWhiteSpace(e.MatchKey));
            Assert.False(string.IsNullOrWhiteSpace(e.M0FindingId));
            Assert.False(string.IsNullOrWhiteSpace(e.Owner));
            Assert.False(string.IsNullOrWhiteSpace(e.Disposition));
            Assert.False(string.IsNullOrWhiteSpace(e.Rationale));
            Assert.False(string.IsNullOrWhiteSpace(e.RemovalBoundary));
            Assert.Contains(e.Category, allowedCategories);
            Assert.StartsWith("R61-AL-", e.FindingId, StringComparison.Ordinal);
            Assert.StartsWith("R61-", e.M0FindingId, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Allowlist_FindingIdSet_IsFrozenAtM3Baseline()
    {
        // Growing the allowlist requires an explicit update of this frozen set
        // (and plan rationale). Silent growth is a ratchet failure.
        var liveIds = LegacyArchitectureAllowlist.Entries
            .Select(e => e.FindingId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var approvedIds = LegacyArchitectureAllowlist.ApprovedFindingIds
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(approvedIds, liveIds);

        // M3 baseline size: 5 namespace edges + 4 locator sites + 0 root admissions.
        Assert.Equal(9, liveIds.Length);
        Assert.Equal(
            5,
            LegacyArchitectureAllowlist.EntriesForCategory(ArchitectureRatchet.CategoryNamespaceDirection).Count);
        Assert.Equal(
            4,
            LegacyArchitectureAllowlist.EntriesForCategory(ArchitectureRatchet.CategoryLocatorSite).Count);
        Assert.Empty(
            LegacyArchitectureAllowlist.EntriesForCategory(ArchitectureRatchet.CategoryRootFolderAdmission));
    }

    [Fact]
    public void Allowlist_EveryEntryMapsToM0FindingOrDocumentedDeferredException()
    {
        // M3 seeds only from M0 IDs that have executable inventory support for
        // the three ratchet categories. Deferred lifetime IDs (R61-V15/16/18-20
        // and R61-LT01-03) are documented debt without M3 executable edges here.
        var approvedM0Ids = new[]
        {
            "R61-V02",
            "R61-V05",
            "R61-V06",
            "R61-V07",
            "R61-V08",
            "R61-V09",
        };

        Assert.All(
            LegacyArchitectureAllowlist.Entries,
            e => Assert.Contains(e.M0FindingId, approvedM0Ids));
    }

    [Fact]
    public void NamespaceDirection_NoNewViolations_AndAllowlistFullyExercised()
    {
        var inventory = ReadInventoryOrFail();
        var live = ArchitectureRatchet.DetectNamespaceDirectionViolations(inventory);
        var allowlist = LegacyArchitectureAllowlist.EntriesForCategory(
            ArchitectureRatchet.CategoryNamespaceDirection);

        AssertRatchet(live, allowlist, ArchitectureRatchet.CategoryNamespaceDirection);

        // Known accepted debt sites remain visible (not silently dropped).
        Assert.Contains(live, v => v.RelativePath == "src/Features/SourceControl/Domain/SourceControlState.cs");
        Assert.Contains(live, v => v.RelativePath == "src/Services/ITerminalSessionFactory.cs");
        Assert.Contains(live, v => v.RelativePath == "src/Services/TerminalSessionFactory.cs");
        Assert.Contains(live, v => v.RelativePath == "src/Services/MentionParser.cs");
        Assert.Contains(live, v => v.RelativePath == "src/Features/SourceControl/Application/SourceControlDiffTabService.cs");
        Assert.Equal(5, live.Count);
    }

    [Fact]
    public void LocatorSites_NoNewSites_AndAllowlistFullyExercised()
    {
        var inventory = ReadInventoryOrFail();
        var live = ArchitectureRatchet.DetectLocatorSiteViolations(inventory);
        var allowlist = LegacyArchitectureAllowlist.EntriesForCategory(
            ArchitectureRatchet.CategoryLocatorSite);

        AssertRatchet(live, allowlist, ArchitectureRatchet.CategoryLocatorSite);

        Assert.Contains(live, v => v.RelativePath == "src/Program.cs");
        Assert.Contains(live, v => v.RelativePath == "src/App.axaml.cs");
        Assert.Contains(live, v => v.RelativePath == "src/Features/SourceControl/Application/SourceControlDiffTabService.cs");
        Assert.Contains(live, v => v.RelativePath == "src/Features/Editor/Presentation/EditorTabViewModel.cs");
        Assert.Equal(4, live.Count);

        // No View-layer locator site today; ratchet keeps it that way.
        Assert.DoesNotContain(
            live,
            v => v.RelativePath.StartsWith("src/Views/", StringComparison.Ordinal));
    }

    [Fact]
    public void RootFolderAdmission_DenyByDefault_NoUnallowlistedAdmissions()
    {
        var inventory = ReadInventoryOrFail();
        var live = ArchitectureRatchet.DetectRootFolderAdmissionViolations(inventory);
        var allowlist = LegacyArchitectureAllowlist.EntriesForCategory(
            ArchitectureRatchet.CategoryRootFolderAdmission);

        AssertRatchet(live, allowlist, ArchitectureRatchet.CategoryRootFolderAdmission);

        // M0/M2 baseline: no tracked production C# under Infrastructure or UI/Shared.
        // Non-C# assets are outside this inventory/ratchet.
        Assert.Empty(live);
        Assert.Empty(allowlist);
        Assert.DoesNotContain(
            inventory.RootFolderAdmissionEvidence,
            e => e.IsUnderRootInfrastructure || e.IsUnderUiShared);
    }

    [Fact]
    public void CombinedRatchet_NoNewViolationsOutsideAllowlist()
    {
        var inventory = ReadInventoryOrFail();
        var live = ArchitectureRatchet.DetectAllRatchetedViolations(inventory);
        var allowlist = LegacyArchitectureAllowlist.Entries;

        AssertRatchet(live, allowlist, "Combined");

        // Known accepted legacy debt is present; it is not treated as failure.
        Assert.Equal(allowlist.Count, live.Count);
        Assert.Equal(
            allowlist.Select(e => e.MatchKey).OrderBy(k => k, StringComparer.Ordinal),
            live.Select(v => v.MatchKey).OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public void InventoryFailure_IsDistinguishedFromViolationFailures()
    {
        // Tooling / inventory failures must surface with INVENTORY_FAILURE, not
        // as a silent empty allowlist pass.
        try
        {
            var inventory = new ArchitectureInventoryReader().Read();
            Assert.NotNull(inventory);
            Assert.NotEmpty(inventory.SourceFiles);
            Assert.NotEmpty(inventory.Types);
            Assert.NotEmpty(inventory.Findings);
        }
        catch (Exception ex)
        {
            Assert.Fail(
                $"{ArchitectureRatchet.FailureInventory}: hybrid inventory could not be read. " +
                $"This is a tooling failure, not a legacy allowlist hit. {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static ArchitectureInventory ReadInventoryOrFail()
    {
        try
        {
            return new ArchitectureInventoryReader().Read();
        }
        catch (Exception ex)
        {
            Assert.Fail(
                $"{ArchitectureRatchet.FailureInventory}: hybrid inventory could not be read. " +
                $"This is a tooling failure, not a legacy allowlist hit. {ex.GetType().Name}: {ex.Message}");
            throw; // unreachable; keeps compiler happy
        }
    }

    private static void AssertRatchet(
        System.Collections.Generic.IReadOnlyList<ArchitectureViolation> live,
        System.Collections.Generic.IReadOnlyList<ArchitectureAllowlistEntry> allowlist,
        string scope)
    {
        var novel = ArchitectureRatchet.FindNewViolations(live, allowlist);
        var stale = ArchitectureRatchet.FindUnexercisedAllowlistEntries(live, allowlist);

        if (novel.Count > 0)
        {
            Assert.Fail(
                $"{ArchitectureRatchet.FailureNewViolation} ({scope}): " +
                $"found {novel.Count} violation(s) outside the approved legacy allowlist." +
                Environment.NewLine +
                ArchitectureRatchet.FormatNewViolations(novel) +
                Environment.NewLine +
                "Do not grow the allowlist to hide new debt without review; fix the new site " +
                "or follow LegacyArchitectureAllowlist mutation rule (Add).");
        }

        if (stale.Count > 0)
        {
            Assert.Fail(
                $"{ArchitectureRatchet.FailureStaleAllowlist} ({scope}): " +
                $"{stale.Count} allowlist entr(y/ies) have no live inventory evidence." +
                Environment.NewLine +
                ArchitectureRatchet.FormatStaleAllowlist(stale));
        }
    }
}
