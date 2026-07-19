using System;
using System.Linq;
using Xunit;

namespace Zaide.Tests.Architecture;

/// <summary>
/// Refactor 6.1 M4: public/internal visibility baseline and expanded root-folder
/// admission ratchets. Preserves M3 legacy allowlist behavior.
/// </summary>
public sealed class ArchitectureVisibilityTests
{
    [Fact]
    public void VisibilityBaseline_MatchesCompiledCountsAndFrozenFullNames()
    {
        var inventory = ReadInventoryOrFail();
        var approved = LoadBaselineOrFail(inventory.RepositoryRoot);
        var livePublic = ArchitectureVisibilityRatchet.GetLivePublicFullNames(inventory);

        Assert.Equal(PublicProductionTypeBaseline.TotalTopLevelTypes, inventory.TotalTopLevelTypeCount);
        Assert.Equal(PublicProductionTypeBaseline.PublicTopLevelTypes, inventory.PublicTopLevelTypeCount);
        Assert.Equal(PublicProductionTypeBaseline.InternalTopLevelTypes, inventory.InternalTopLevelTypeCount);
        Assert.Equal(
            PublicProductionTypeBaseline.PublicTopLevelTypes +
            PublicProductionTypeBaseline.InternalTopLevelTypes,
            inventory.TotalTopLevelTypeCount);

        Assert.Equal(PublicProductionTypeBaseline.PublicTopLevelTypes, approved.Count);
        Assert.Equal(PublicProductionTypeBaseline.PublicTopLevelTypes, livePublic.Count);
        Assert.Equal(approved, livePublic);
    }

    [Fact]
    public void PublicByException_NoNewPublicTypeOutsideBaseline()
    {
        var inventory = ReadInventoryOrFail();
        var approved = LoadBaselineOrFail(inventory.RepositoryRoot);
        var livePublic = ArchitectureVisibilityRatchet.GetLivePublicFullNames(inventory);

        var novel = ArchitectureVisibilityRatchet.FindNewPublicTypes(livePublic, approved);
        if (novel.Count > 0)
        {
            Assert.Fail(
                $"{ArchitectureVisibilityRatchet.FailureNewPublicType}: " +
                $"found {novel.Count} public production type(s) outside the approved baseline." +
                Environment.NewLine +
                ArchitectureVisibilityRatchet.FormatNewPublicTypes(novel));
        }
    }

    [Fact]
    public void PublicByException_NoStaleBaselineEntries()
    {
        var inventory = ReadInventoryOrFail();
        var approved = LoadBaselineOrFail(inventory.RepositoryRoot);
        var livePublic = ArchitectureVisibilityRatchet.GetLivePublicFullNames(inventory);

        var stale = ArchitectureVisibilityRatchet.FindStalePublicBaselineNames(livePublic, approved);
        if (stale.Count > 0)
        {
            Assert.Fail(
                $"{ArchitectureVisibilityRatchet.FailureStalePublicBaseline}: " +
                $"{stale.Count} baseline name(s) have no live public type." +
                Environment.NewLine +
                ArchitectureVisibilityRatchet.FormatStalePublicBaseline(stale));
        }
    }

    [Fact]
    public void VisibilityBaseline_FileIsDeterministicAndIntegrityChecked()
    {
        var root = ArchitectureInventoryReader.ResolveRepositoryRoot();

        // Load enforces: present, non-empty lines, no comments, unique, sorted,
        // exact count == PublicTopLevelTypes. Any violation is VISIBILITY_BASELINE_INTEGRITY.
        System.Collections.Generic.IReadOnlyList<string> names;
        try
        {
            names = PublicProductionTypeBaseline.LoadApprovedPublicFullNames(root);
        }
        catch (Exception ex)
        {
            Assert.Fail(
                $"{ArchitectureVisibilityRatchet.FailureVisibilityBaselineIntegrity}: " +
                $"could not load public type baseline. {ex.GetType().Name}: {ex.Message}");
            throw;
        }

        Assert.Equal(PublicProductionTypeBaseline.PublicTopLevelTypes, names.Count);
        Assert.Equal(
            names.OrderBy(n => n, StringComparer.Ordinal),
            names);
        Assert.Equal(
            names.Count,
            names.Distinct(StringComparer.Ordinal).Count());
        Assert.All(names, n =>
        {
            Assert.False(string.IsNullOrWhiteSpace(n));
            Assert.StartsWith("Zaide", n, StringComparison.Ordinal);
        });

        // Count constants must stay aligned with M0/M2 reader constants.
        Assert.Equal(
            ArchitectureInventoryReader.M0TotalTopLevelTypes,
            PublicProductionTypeBaseline.TotalTopLevelTypes);
        Assert.Equal(
            ArchitectureInventoryReader.M0PublicTopLevelTypes,
            PublicProductionTypeBaseline.PublicTopLevelTypes);
        Assert.Equal(
            ArchitectureInventoryReader.M0InternalTopLevelTypes,
            PublicProductionTypeBaseline.InternalTopLevelTypes);
    }

    [Fact]
    public void ExpandedRootFolderAdmission_DenyUnauthorizedFoldersAndRoots()
    {
        var inventory = ReadInventoryOrFail();
        var live = ArchitectureVisibilityRatchet.DetectExpandedRootFolderAdmissionViolations(inventory);
        var allowlist = LegacyArchitectureAllowlist.EntriesForCategory(
            ArchitectureRatchet.CategoryRootFolderAdmission);

        // Same allowlist mutation rules as M3; empty root-admission allowlist today.
        var novel = ArchitectureRatchet.FindNewViolations(live, allowlist);
        var stale = ArchitectureRatchet.FindUnexercisedAllowlistEntries(live, allowlist);

        if (novel.Count > 0)
        {
            Assert.Fail(
                $"{ArchitectureRatchet.FailureNewViolation} (ExpandedRootFolderAdmission): " +
                $"found {novel.Count} unauthorized root-folder admission(s)." +
                Environment.NewLine +
                ArchitectureRatchet.FormatNewViolations(novel));
        }

        if (stale.Count > 0)
        {
            Assert.Fail(
                $"{ArchitectureRatchet.FailureStaleAllowlist} (ExpandedRootFolderAdmission): " +
                Environment.NewLine +
                ArchitectureRatchet.FormatStaleAllowlist(stale));
        }

        // Current inventory: no unauthorized admissions.
        Assert.Empty(live);
        Assert.Empty(allowlist);

        // M3 deny-by-default paths remain clean.
        Assert.DoesNotContain(
            inventory.RootFolderAdmissionEvidence,
            e => e.IsUnderRootInfrastructure || e.IsUnderUiShared);

        // Completed feature-first tree (M12): App Composition/Shell, UI DesignSystem,
        // Features (all migrated). No technical-layer folders or src root composition.
        Assert.All(inventory.SourceFiles, f =>
        {
            var path = f.RelativePath.Replace('\\', '/');
            if (f.TechnicalFolder == "src")
            {
                Assert.Contains(
                    path,
                    ArchitectureVisibilityRatchet.ApprovedSrcRootCompositionFiles);
            }
            else if (f.TechnicalFolder == "App")
            {
                Assert.True(
                    ArchitectureVisibilityRatchet.IsApprovedAppPath(path),
                    $"App path not admitted: {path}");
            }
            else if (f.TechnicalFolder == "UI")
            {
                Assert.True(
                    ArchitectureVisibilityRatchet.IsApprovedUiPath(path),
                    $"UI path not admitted: {path}");
            }
            else if (f.TechnicalFolder == "Features")
            {
                Assert.True(
                    ArchitectureVisibilityRatchet.IsApprovedFeaturesPath(path),
                    $"Features path not admitted: {path}");
            }
            else
            {
                Assert.Contains(
                    f.TechnicalFolder,
                    ArchitectureVisibilityRatchet.ApprovedCurrentTechnicalFolders);
            }
        });

        Assert.Equal(0, inventory.SourceFiles.Count(f => f.TechnicalFolder == "src"));
        // Refactor 8 M3: +1 RightColumnHost production file (was 38 after M2).
        // Refactor 8 M4: +1 MainLayoutBuilder production file.
        Assert.Equal(40, inventory.SourceFiles.Count(f => f.TechnicalFolder == "App"));
        Assert.Equal(4, inventory.SourceFiles.Count(f => f.TechnicalFolder == "UI"));
        // Refactor 7 M5b: +1 output projection production file.
        Assert.Equal(366, inventory.SourceFiles.Count(f => f.TechnicalFolder == "Features"));
    }

    [Fact]
    public void ExpandedRootFolderAdmission_PreservesM3InfrastructureAndUiSharedRules()
    {
        // M3 DetectRootFolderAdmissionViolations remains the authoritative
        // Infrastructure / UI.Shared detector; expanded rules must not replace it.
        var inventory = ReadInventoryOrFail();
        var m3 = ArchitectureRatchet.DetectRootFolderAdmissionViolations(inventory);
        var expanded = ArchitectureVisibilityRatchet.DetectExpandedRootFolderAdmissionViolations(inventory);

        Assert.Empty(m3);
        Assert.Empty(expanded);

        // M3 category and failure prefixes still defined and used by ratchet tests.
        Assert.Equal("RootFolderAdmission", ArchitectureRatchet.CategoryRootFolderAdmission);
        Assert.Equal("NEW_VIOLATION", ArchitectureRatchet.FailureNewViolation);
        Assert.Equal("STALE_ALLOWLIST", ArchitectureRatchet.FailureStaleAllowlist);
    }

    [Fact]
    public void M5LegacyAllowlist_ContainsOnlyCompositionLocatorResiduals()
    {
        // Hard boundary: later visibility work must not alter legacy allowlist to pass.
        // M5 residual: 2 FindingIds (0 NS + 2 LOC) after SourceControlState deletion.
        Assert.Equal(2, LegacyArchitectureAllowlist.Entries.Count);
        Assert.Equal(2, LegacyArchitectureAllowlist.ApprovedFindingIds.Count);
        Assert.Equal(
            0,
            LegacyArchitectureAllowlist.EntriesForCategory(
                ArchitectureRatchet.CategoryNamespaceDirection).Count);
        Assert.Equal(
            2,
            LegacyArchitectureAllowlist.EntriesForCategory(
                ArchitectureRatchet.CategoryLocatorSite).Count);
        Assert.Empty(
            LegacyArchitectureAllowlist.EntriesForCategory(
                ArchitectureRatchet.CategoryRootFolderAdmission));

        Assert.Equal(
            LegacyArchitectureAllowlist.ApprovedFindingIds
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray(),
            LegacyArchitectureAllowlist.Entries
                .Select(e => e.FindingId)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray());
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
                $"This is a tooling failure, not a visibility baseline hit. " +
                $"{ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    private static System.Collections.Generic.IReadOnlyList<string> LoadBaselineOrFail(
        string repositoryRoot)
    {
        try
        {
            return PublicProductionTypeBaseline.LoadApprovedPublicFullNames(repositoryRoot);
        }
        catch (Exception ex)
        {
            Assert.Fail(
                $"{ArchitectureVisibilityRatchet.FailureVisibilityBaselineIntegrity}: " +
                $"public type baseline could not be loaded. {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }
}
