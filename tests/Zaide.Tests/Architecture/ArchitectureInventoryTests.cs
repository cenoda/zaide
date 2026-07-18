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

        // Namespace rollups after Refactor 6.2 M1–M12 (completed feature-first tree:
        // DesignSystem, all Features, App Composition + Shell).
        var byNamespace = inventory.TypeCountByNamespace;
        Assert.False(byNamespace.ContainsKey("Zaide"));
        Assert.False(byNamespace.ContainsKey("Zaide.Models"));
        Assert.False(byNamespace.ContainsKey("Zaide.Services"));
        Assert.False(byNamespace.ContainsKey("Zaide.ViewModels"));
        Assert.False(byNamespace.ContainsKey("Zaide.Views"));
        Assert.Equal((6, 5, 1), byNamespace["Zaide.App.Composition"]);
        Assert.Equal((3, 0, 3), byNamespace["Zaide.App.Composition.Registration"]);
        Assert.Equal((16, 14, 2), byNamespace["Zaide.App.Shell"]);
        Assert.Equal((2, 2, 0), byNamespace["Zaide.UI.DesignSystem"]);
        Assert.Equal((11, 11, 0), byNamespace["Zaide.Features.Settings.Domain"]);
        Assert.Equal((3, 3, 0), byNamespace["Zaide.Features.Settings.Contracts"]);
        Assert.Equal((7, 6, 1), byNamespace["Zaide.Features.Settings.Infrastructure"]);
        Assert.Equal((7, 5, 2), byNamespace["Zaide.Features.Settings.Presentation"]);
        Assert.Equal((2, 2, 0), byNamespace["Zaide.Features.Workspace.Domain"]);
        Assert.Equal((1, 1, 0), byNamespace["Zaide.Features.Workspace.Contracts"]);
        Assert.Equal((1, 1, 0), byNamespace["Zaide.Features.Workspace.Infrastructure"]);
        Assert.Equal((3, 2, 1), byNamespace["Zaide.Features.Workspace.Presentation"]);
        Assert.Equal((6, 6, 0), byNamespace["Zaide.Features.Editor.Domain"]);
        Assert.Equal((6, 6, 0), byNamespace["Zaide.Features.Editor.Contracts"]);
        Assert.Equal((1, 0, 1), byNamespace["Zaide.Features.Editor.Infrastructure"]);
        Assert.Equal((17, 13, 4), byNamespace["Zaide.Features.Editor.Presentation"]);
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
        Assert.Equal((6, 6, 0), byNamespace["Zaide.Features.SourceControl.Domain"]);
        Assert.Equal((5, 5, 0), byNamespace["Zaide.Features.SourceControl.Contracts"]);
        Assert.Equal((14, 14, 0), byNamespace["Zaide.Features.SourceControl.Application"]);
        Assert.Equal((3, 1, 2), byNamespace["Zaide.Features.SourceControl.Infrastructure"]);
        Assert.Equal((2, 2, 0), byNamespace["Zaide.Features.SourceControl.Presentation"]);
        Assert.Equal((2, 2, 0), byNamespace["Zaide.Features.Terminal.Contracts"]);
        Assert.False(byNamespace.ContainsKey("Zaide.Features.Terminal.Application"));
        Assert.Equal((3, 1, 2), byNamespace["Zaide.Features.Terminal.Infrastructure"]);
        Assert.Equal((35, 19, 16), byNamespace["Zaide.Features.Terminal.Presentation"]);
        Assert.Equal((7, 7, 0), byNamespace["Zaide.Features.Townhall.Domain"]);
        Assert.Equal((7, 7, 0), byNamespace["Zaide.Features.Townhall.Presentation"]);
        Assert.Equal((3, 3, 0), byNamespace["Zaide.Features.Agents.Domain"]);
        Assert.Equal((3, 3, 0), byNamespace["Zaide.Features.Agents.Contracts"]);
        Assert.Equal((5, 5, 0), byNamespace["Zaide.Features.Agents.Application"]);
        Assert.Equal((1, 1, 0), byNamespace["Zaide.Features.Agents.Infrastructure"]);
        Assert.Equal((5, 4, 1), byNamespace["Zaide.Features.Agents.Presentation"]);
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

        // Post-M1+M2: 356 base → 358 (M1) → 360 (M2); M5 −1; M6a +1; M6b +1; M6c +1 Workspace module.
        Assert.Equal(362, inventory.SourceFiles.Count);
        Assert.False(byFolder.ContainsKey("src"));
        Assert.False(byFolder.ContainsKey("Models"));
        Assert.False(byFolder.ContainsKey("Services"));
        Assert.False(byFolder.ContainsKey("ViewModels"));
        Assert.False(byFolder.ContainsKey("Views"));
        Assert.False(byFolder.ContainsKey("Styles"));
        Assert.Equal(23, byFolder["App"]);
        Assert.Equal(2, byFolder["UI"]);
        Assert.Equal(337, byFolder["Features"]);

        // Namespace declarations match the completed feature-first tree
        // (Refactor 6.2 M1–M12: App Composition/Shell, UI DesignSystem, Features;
        // M6a adds Composition.Registration).
        Assert.All(
            inventory.SourceFiles.Where(s => s.TechnicalFolder == "App"),
            s =>
            {
                var path = s.RelativePath.Replace('\\', '/');
                Assert.True(
                    path.StartsWith("src/App/Composition/", StringComparison.Ordinal)
                    || path.StartsWith("src/App/Shell/", StringComparison.Ordinal),
                    $"Unexpected App path: {path}");
                Assert.True(
                    s.DeclaredNamespace is "Zaide.App.Composition"
                        or "Zaide.App.Composition.Registration"
                        or "Zaide.App.Shell",
                    $"Unexpected App namespace: {s.DeclaredNamespace}");
            });
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
                    || path.StartsWith("src/Features/Debugging/", StringComparison.Ordinal)
                    || path.StartsWith("src/Features/SourceControl/", StringComparison.Ordinal)
                    || path.StartsWith("src/Features/Terminal/", StringComparison.Ordinal)
                    || path.StartsWith("src/Features/Townhall/", StringComparison.Ordinal)
                    || path.StartsWith("src/Features/Agents/", StringComparison.Ordinal),
                    $"Unexpected Features path: {path}");
                Assert.True(
                    s.DeclaredNamespace.StartsWith("Zaide.Features.Settings.", StringComparison.Ordinal)
                    || s.DeclaredNamespace.StartsWith("Zaide.Features.Workspace.", StringComparison.Ordinal)
                    || s.DeclaredNamespace.StartsWith("Zaide.Features.Editor.", StringComparison.Ordinal)
                    || s.DeclaredNamespace.StartsWith("Zaide.Features.ProjectSystem.", StringComparison.Ordinal)
                    || s.DeclaredNamespace.StartsWith("Zaide.Features.Language.", StringComparison.Ordinal)
                    || s.DeclaredNamespace.StartsWith("Zaide.Features.Debugging.", StringComparison.Ordinal)
                    || s.DeclaredNamespace.StartsWith("Zaide.Features.SourceControl.", StringComparison.Ordinal)
                    || s.DeclaredNamespace.StartsWith("Zaide.Features.Terminal.", StringComparison.Ordinal)
                    || s.DeclaredNamespace.StartsWith("Zaide.Features.Townhall.", StringComparison.Ordinal)
                    || s.DeclaredNamespace.StartsWith("Zaide.Features.Agents.", StringComparison.Ordinal),
                    $"Unexpected Features namespace: {s.DeclaredNamespace}");
            });
    }

    [Fact]
    public void Read_ProviderEvidence_IncludesKnownLocatorSites_WithoutFailingOnThem()
    {
        var inventory = new ArchitectureInventoryReader().Read();

        // Presence inventory only — known M0 locator debt must remain visible,
        // not turn red under M2.
        Assert.Contains(
            inventory.ProviderEvidence,
            e => e.RelativePath == "src/App/Composition/Program.cs"
                && e.Kind == ProviderEvidenceEntry.KindAppServices);
        Assert.Contains(
            inventory.ProviderEvidence,
            e => e.RelativePath == "src/App/Composition/App.axaml.cs"
                && e.Kind == ProviderEvidenceEntry.KindGetRequiredService);
        // M2 cleared SourceControlDiffTabService IServiceProvider evidence.
        Assert.DoesNotContain(
            inventory.ProviderEvidence,
            e => e.RelativePath == "src/Features/SourceControl/Application/SourceControlDiffTabService.cs");

        var resolutionCalls = inventory.ProviderEvidence.Count(e =>
            e.Kind is ProviderEvidenceEntry.KindGetRequiredService
                or ProviderEvidenceEntry.KindGetService);
        Assert.True(
            resolutionCalls >= 35,
            $"Expected at least 35 resolution call expressions (M2 floor); found {resolutionCalls}.");
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
        // M5 cleared the last NamespaceDependencyEvidence residual (SourceControlState).
        Assert.DoesNotContain(inventory.Findings, f => f.Kind == "NamespaceDependencyEvidence");
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
        // M3 cleared Terminal session factory → Presentation (V05).
        // M5 deleted SourceControlState Domain → Application residual (V02).
        Assert.DoesNotContain(
            inventory.NamespaceDependencyEvidence,
            e => e.RelativePath.Contains("TerminalSessionFactory", StringComparison.Ordinal)
                || e.RelativePath.Contains("ITerminalSessionFactory", StringComparison.Ordinal));
        Assert.DoesNotContain(
            inventory.NamespaceDependencyEvidence,
            e => e.RelativePath.Contains("SourceControlState", StringComparison.Ordinal));
        Assert.Empty(inventory.NamespaceDependencyEvidence);
    }
}
