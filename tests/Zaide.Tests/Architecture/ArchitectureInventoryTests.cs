using System;
using System.Linq;
using Xunit;

namespace Zaide.Tests.Architecture;

/// <summary>
/// Refactor 6.1 M2: architecture inventory harness tests.
/// Proves determinism and the M0 visibility baseline. Does not enforce
/// allowlists, ratchets, or fail on known legacy violations.
/// </summary>
public sealed class ArchitectureInventoryTests
{
    [Fact]
    public void Read_ReproducesM0TopLevelVisibilityBaseline()
    {
        var inventory = new ArchitectureInventoryReader().Read();

        Assert.Equal(
            ArchitectureInventoryReader.M0TotalTopLevelTypes,
            inventory.TotalTopLevelTypeCount);
        Assert.Equal(
            ArchitectureInventoryReader.M0PublicTopLevelTypes,
            inventory.PublicTopLevelTypeCount);
        Assert.Equal(
            ArchitectureInventoryReader.M0InternalTopLevelTypes,
            inventory.InternalTopLevelTypeCount);

        Assert.Equal(
            ArchitectureInventoryReader.M0PublicTopLevelTypes +
            ArchitectureInventoryReader.M0InternalTopLevelTypes,
            inventory.TotalTopLevelTypeCount);

        // Namespace rollups after Refactor 6.2 M1 (DesignSystem) + M2 (Settings).
        var byNamespace = inventory.TypeCountByNamespace;
        Assert.Equal((3, 2, 1), byNamespace["Zaide"]);
        Assert.Equal((20, 20, 0), byNamespace["Zaide.Models"]);
        Assert.Equal((221, 205, 16), byNamespace["Zaide.Services"]);
        Assert.Equal((2, 2, 0), byNamespace["Zaide.UI.DesignSystem"]);
        Assert.Equal((71, 57, 14), byNamespace["Zaide.ViewModels"]);
        Assert.Equal((48, 37, 11), byNamespace["Zaide.Views"]);
        Assert.Equal((11, 11, 0), byNamespace["Zaide.Features.Settings.Domain"]);
        Assert.Equal((3, 3, 0), byNamespace["Zaide.Features.Settings.Contracts"]);
        Assert.Equal((7, 6, 1), byNamespace["Zaide.Features.Settings.Infrastructure"]);
        Assert.Equal((7, 5, 2), byNamespace["Zaide.Features.Settings.Presentation"]);
        Assert.False(byNamespace.ContainsKey("Zaide.Styles"));
    }

    [Fact]
    public void Read_IsDeterministic_AcrossIndependentReads()
    {
        var readerA = new ArchitectureInventoryReader();
        var readerB = new ArchitectureInventoryReader();

        var first = readerA.Read();
        var second = readerB.Read();

        Assert.Equal(first, second);
        Assert.Equal(
            first.Findings.Select(f => f.StableKey).ToArray(),
            second.Findings.Select(f => f.StableKey).ToArray());
        Assert.Equal(
            first.Types.Select(t => t.FullName).ToArray(),
            second.Types.Select(t => t.FullName).ToArray());
        Assert.Equal(
            first.SourceFiles.Select(s => s.RelativePath).ToArray(),
            second.SourceFiles.Select(s => s.RelativePath).ToArray());
    }

    [Fact]
    public void Read_SourceFolderPlacement_MatchesM0TrackedFileCounts()
    {
        var inventory = new ArchitectureInventoryReader().Read();
        var byFolder = inventory.SourceFileCountByTechnicalFolder;

        Assert.Equal(356, inventory.SourceFiles.Count);
        Assert.Equal(3, byFolder["src"]);
        Assert.Equal(15, byFolder["Models"]);
        Assert.Equal(214, byFolder["Services"]);
        Assert.Equal(2, byFolder["UI"]);
        Assert.Equal(24, byFolder["Features"]);
        Assert.False(byFolder.ContainsKey("Styles"));
        Assert.Equal(52, byFolder["ViewModels"]);
        Assert.Equal(46, byFolder["Views"]);

        // Namespace declarations match folders for the current mixed tree
        // (technical layers plus Refactor 6.2 M1 DesignSystem and M2 Settings).
        Assert.All(
            inventory.SourceFiles.Where(s => s.TechnicalFolder == "src"),
            s => Assert.Equal("Zaide", s.DeclaredNamespace));
        Assert.All(
            inventory.SourceFiles.Where(s => s.TechnicalFolder == "Models"),
            s => Assert.Equal("Zaide.Models", s.DeclaredNamespace));
        Assert.All(
            inventory.SourceFiles.Where(s => s.TechnicalFolder == "Services"),
            s => Assert.Equal("Zaide.Services", s.DeclaredNamespace));
        Assert.All(
            inventory.SourceFiles.Where(s => s.TechnicalFolder == "UI"),
            s =>
            {
                Assert.StartsWith("src/UI/DesignSystem/", s.RelativePath.Replace('\\', '/'), StringComparison.Ordinal);
                Assert.Equal("Zaide.UI.DesignSystem", s.DeclaredNamespace);
            });
        Assert.All(
            inventory.SourceFiles.Where(s => s.TechnicalFolder == "Features"),
            s =>
            {
                var path = s.RelativePath.Replace('\\', '/');
                Assert.StartsWith("src/Features/Settings/", path, StringComparison.Ordinal);
                Assert.StartsWith("Zaide.Features.Settings.", s.DeclaredNamespace, StringComparison.Ordinal);
            });
        Assert.All(
            inventory.SourceFiles.Where(s => s.TechnicalFolder == "ViewModels"),
            s => Assert.Equal("Zaide.ViewModels", s.DeclaredNamespace));
        Assert.All(
            inventory.SourceFiles.Where(s => s.TechnicalFolder == "Views"),
            s => Assert.Equal("Zaide.Views", s.DeclaredNamespace));
    }

    [Fact]
    public void Read_ProviderEvidence_IncludesKnownLocatorSites_WithoutFailingOnThem()
    {
        var inventory = new ArchitectureInventoryReader().Read();

        // Presence inventory only — known M0 locator debt must remain visible,
        // not turn red under M2.
        Assert.Contains(
            inventory.ProviderEvidence,
            e => e.RelativePath == "src/Program.cs"
                && e.Kind == ProviderEvidenceEntry.KindAppServices);
        Assert.Contains(
            inventory.ProviderEvidence,
            e => e.RelativePath == "src/App.axaml.cs"
                && e.Kind == ProviderEvidenceEntry.KindGetRequiredService);
        Assert.Contains(
            inventory.ProviderEvidence,
            e => e.RelativePath == "src/Services/SourceControlDiffTabService.cs"
                && e.Kind == ProviderEvidenceEntry.KindIServiceProvider);
        Assert.Contains(
            inventory.ProviderEvidence,
            e => e.RelativePath == "src/ViewModels/EditorTabViewModel.cs"
                && e.Kind == ProviderEvidenceEntry.KindGetService);

        var resolutionCalls = inventory.ProviderEvidence.Count(e =>
            e.Kind is ProviderEvidenceEntry.KindGetRequiredService
                or ProviderEvidenceEntry.KindGetService);
        Assert.True(
            resolutionCalls >= 44,
            $"Expected at least the M0 44 resolution call expressions; found {resolutionCalls}.");
    }

    [Fact]
    public void Read_Findings_AreStableSortedAndCoverHybridInventory()
    {
        var inventory = new ArchitectureInventoryReader().Read();

        Assert.NotEmpty(inventory.Findings);
        Assert.Equal(
            inventory.Findings.OrderBy(f => f.StableKey, StringComparer.Ordinal).Select(f => f.StableKey),
            inventory.Findings.Select(f => f.StableKey));

        Assert.Contains(inventory.Findings, f => f.Kind == "ProductionSourceFile");
        Assert.Contains(inventory.Findings, f => f.Kind == "ProductionType");
        Assert.Contains(inventory.Findings, f => f.Kind == "ProviderEvidence");
        Assert.Contains(inventory.Findings, f => f.Kind == "NamespaceDependencyEvidence");
        Assert.Contains(inventory.Findings, f => f.Kind == "RootFolderAdmissionEvidence");

        // Public type findings are explicit full-name candidates for later M4 allowlist.
        var publicTypeFindings = inventory.Findings
            .Where(f => f.Kind == "ProductionType" && f.StableKey.StartsWith("type:public:", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(ArchitectureInventoryReader.M0PublicTopLevelTypes, publicTypeFindings.Length);
        Assert.All(publicTypeFindings, f => Assert.StartsWith("type:public:", f.StableKey, StringComparison.Ordinal));

        // Current tree has no target root Infrastructure/ or UI/Shared admissions.
        Assert.DoesNotContain(
            inventory.RootFolderAdmissionEvidence,
            e => e.IsUnderRootInfrastructure || e.IsUnderUiShared);
    }

    [Fact]
    public void Read_NamespaceDependencyEvidence_IncludesVerifiedForbiddenLocations()
    {
        var inventory = new ArchitectureInventoryReader().Read();

        // Inventory of known M0 forbidden locations — not a failure ratchet.
        Assert.Contains(
            inventory.NamespaceDependencyEvidence,
            e => e.RelativePath.Contains("TerminalSessionFactory", StringComparison.Ordinal)
                && e.SourceTechnicalFolder == "Services"
                && e.TargetNamespaceFragment == "Zaide.ViewModels");
        Assert.Contains(
            inventory.NamespaceDependencyEvidence,
            e => e.RelativePath.Contains("SourceControlState", StringComparison.Ordinal)
                && e.SourceTechnicalFolder == "Models"
                && e.TargetNamespaceFragment == "Zaide.Services");
    }
}
