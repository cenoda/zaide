using System;
using System.Linq;
using Xunit;
using Zaide.Features.Editor.Presentation;

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

        // Namespace rollups after Refactor 6.2 M1–M7c (DesignSystem, Settings, Workspace,
        // Editor, ProjectSystem, Language application/contracts + Lsp, Debugging
        // application/contracts + Dap + Presentation).
        var byNamespace = inventory.TypeCountByNamespace;
        Assert.Equal((3, 2, 1), byNamespace["Zaide"]);
        Assert.Equal((17, 17, 0), byNamespace["Zaide.Models"]);
        Assert.Equal((36, 35, 1), byNamespace["Zaide.Services"]);
        Assert.Equal((2, 2, 0), byNamespace["Zaide.UI.DesignSystem"]);
        Assert.Equal((40, 27, 13), byNamespace["Zaide.ViewModels"]);
        Assert.Equal((28, 22, 6), byNamespace["Zaide.Views"]);
        Assert.Equal((11, 11, 0), byNamespace["Zaide.Features.Settings.Domain"]);
        Assert.Equal((3, 3, 0), byNamespace["Zaide.Features.Settings.Contracts"]);
        Assert.Equal((7, 6, 1), byNamespace["Zaide.Features.Settings.Infrastructure"]);
        Assert.Equal((7, 5, 2), byNamespace["Zaide.Features.Settings.Presentation"]);
        Assert.Equal((2, 2, 0), byNamespace["Zaide.Features.Workspace.Domain"]);
        Assert.Equal((1, 1, 0), byNamespace["Zaide.Features.Workspace.Contracts"]);
        Assert.Equal((1, 1, 0), byNamespace["Zaide.Features.Workspace.Infrastructure"]);
        Assert.Equal((3, 2, 1), byNamespace["Zaide.Features.Workspace.Presentation"]);
        Assert.Equal((6, 6, 0), byNamespace["Zaide.Features.Editor.Domain"]);
        Assert.Equal((4, 4, 0), byNamespace["Zaide.Features.Editor.Contracts"]);
        Assert.Equal((1, 1, 0), byNamespace["Zaide.Features.Editor.Infrastructure"]);
        Assert.Equal((14, 12, 2), byNamespace["Zaide.Features.Editor.Presentation"]);
        Assert.Equal((35, 35, 0), byNamespace["Zaide.Features.ProjectSystem.Domain"]);
        Assert.Equal((14, 14, 0), byNamespace["Zaide.Features.ProjectSystem.Contracts"]);
        Assert.Equal((13, 13, 0), byNamespace["Zaide.Features.ProjectSystem.Infrastructure"]);
        Assert.Equal((10, 10, 0), byNamespace["Zaide.Features.ProjectSystem.Presentation"]);
        Assert.Equal((8, 8, 0), byNamespace["Zaide.Features.Language.Contracts"]);
        Assert.Equal((47, 42, 5), byNamespace["Zaide.Features.Language.Application"]);
        Assert.Equal((24, 17, 7), byNamespace["Zaide.Features.Language.Infrastructure.Lsp"]);
        Assert.Equal((2, 2, 0), byNamespace["Zaide.Features.Debugging.Contracts"]);
        Assert.Equal((16, 16, 0), byNamespace["Zaide.Features.Debugging.Application"]);
        Assert.Equal((19, 16, 3), byNamespace["Zaide.Features.Debugging.Infrastructure.Dap"]);
        Assert.Equal((19, 16, 3), byNamespace["Zaide.Features.Debugging.Presentation"]);
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
        Assert.Equal(12, byFolder["Models"]);
        Assert.Equal(35, byFolder["Services"]);
        Assert.Equal(2, byFolder["UI"]);
        Assert.Equal(255, byFolder["Features"]);
        Assert.False(byFolder.ContainsKey("Styles"));
        Assert.Equal(23, byFolder["ViewModels"]);
        Assert.Equal(26, byFolder["Views"]);

        // Namespace declarations match folders for the current mixed tree
        // (technical layers plus Refactor 6.2 M1–M7c DesignSystem, Settings, Workspace,
        // Editor, ProjectSystem, Language application/contracts + Lsp, Debugging
        // application/contracts + Dap + Presentation).
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
                Assert.True(
                    path.StartsWith("src/Features/Settings/", StringComparison.Ordinal)
                    || path.StartsWith("src/Features/Workspace/", StringComparison.Ordinal)
                    || path.StartsWith("src/Features/Editor/", StringComparison.Ordinal)
                    || path.StartsWith("src/Features/ProjectSystem/", StringComparison.Ordinal)
                    || path.StartsWith("src/Features/Language/", StringComparison.Ordinal)
                    || path.StartsWith("src/Features/Debugging/", StringComparison.Ordinal),
                    $"Unexpected Features path: {path}");
                Assert.True(
                    s.DeclaredNamespace.StartsWith("Zaide.Features.Settings.", StringComparison.Ordinal)
                    || s.DeclaredNamespace.StartsWith("Zaide.Features.Workspace.", StringComparison.Ordinal)
                    || s.DeclaredNamespace.StartsWith("Zaide.Features.Editor.", StringComparison.Ordinal)
                    || s.DeclaredNamespace.StartsWith("Zaide.Features.ProjectSystem.", StringComparison.Ordinal)
                    || s.DeclaredNamespace.StartsWith("Zaide.Features.Language.", StringComparison.Ordinal)
                    || s.DeclaredNamespace.StartsWith("Zaide.Features.Debugging.", StringComparison.Ordinal),
                    $"Unexpected Features namespace: {s.DeclaredNamespace}");
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
            e => e.RelativePath == "src/Features/Editor/Presentation/EditorTabViewModel.cs"
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
