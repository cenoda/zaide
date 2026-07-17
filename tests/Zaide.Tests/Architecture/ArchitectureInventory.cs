using System;
using System.Collections.Generic;
using System.Linq;

namespace Zaide.Tests.Architecture;

/// <summary>
/// Deterministic hybrid architecture inventory: source placement + compiled
/// visibility + provider/dependency/admission evidence.
/// </summary>
public sealed class ArchitectureInventory : IEquatable<ArchitectureInventory>
{
    public ArchitectureInventory(
        string repositoryRoot,
        IReadOnlyList<ProductionSourceFileEntry> sourceFiles,
        IReadOnlyList<ProductionTypeEntry> types,
        IReadOnlyList<ProviderEvidenceEntry> providerEvidence,
        IReadOnlyList<NamespaceDependencyEvidenceEntry> namespaceDependencyEvidence,
        IReadOnlyList<RootFolderAdmissionEvidenceEntry> rootFolderAdmissionEvidence,
        IReadOnlyList<ArchitectureFinding> findings)
    {
        RepositoryRoot = repositoryRoot ?? throw new ArgumentNullException(nameof(repositoryRoot));
        SourceFiles = sourceFiles ?? throw new ArgumentNullException(nameof(sourceFiles));
        Types = types ?? throw new ArgumentNullException(nameof(types));
        ProviderEvidence = providerEvidence ?? throw new ArgumentNullException(nameof(providerEvidence));
        NamespaceDependencyEvidence = namespaceDependencyEvidence
            ?? throw new ArgumentNullException(nameof(namespaceDependencyEvidence));
        RootFolderAdmissionEvidence = rootFolderAdmissionEvidence
            ?? throw new ArgumentNullException(nameof(rootFolderAdmissionEvidence));
        Findings = findings ?? throw new ArgumentNullException(nameof(findings));
    }

    public string RepositoryRoot { get; }

    public IReadOnlyList<ProductionSourceFileEntry> SourceFiles { get; }

    public IReadOnlyList<ProductionTypeEntry> Types { get; }

    public IReadOnlyList<ProviderEvidenceEntry> ProviderEvidence { get; }

    public IReadOnlyList<NamespaceDependencyEvidenceEntry> NamespaceDependencyEvidence { get; }

    public IReadOnlyList<RootFolderAdmissionEvidenceEntry> RootFolderAdmissionEvidence { get; }

    /// <summary>
    /// Stable finding list for exact allowlists in later milestones.
    /// Sorted by <see cref="ArchitectureFinding.StableKey"/>.
    /// </summary>
    public IReadOnlyList<ArchitectureFinding> Findings { get; }

    public int TotalTopLevelTypeCount => Types.Count;

    public int PublicTopLevelTypeCount => Types.Count(t => t.IsPublic);

    public int InternalTopLevelTypeCount => Types.Count(t => !t.IsPublic);

    public IReadOnlyDictionary<string, int> SourceFileCountByTechnicalFolder =>
        SourceFiles
            .GroupBy(f => f.TechnicalFolder, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

    public IReadOnlyDictionary<string, (int Total, int Public, int Internal)> TypeCountByNamespace =>
        Types
            .GroupBy(t => t.Namespace, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (g.Count(), g.Count(t => t.IsPublic), g.Count(t => !t.IsPublic)),
                StringComparer.Ordinal);

    public bool Equals(ArchitectureInventory? other)
    {
        if (other is null)
        {
            return false;
        }

        return SourceFiles.SequenceEqual(other.SourceFiles)
            && Types.SequenceEqual(other.Types)
            && ProviderEvidence.SequenceEqual(other.ProviderEvidence)
            && NamespaceDependencyEvidence.SequenceEqual(other.NamespaceDependencyEvidence)
            && RootFolderAdmissionEvidence.SequenceEqual(other.RootFolderAdmissionEvidence)
            && Findings.SequenceEqual(other.Findings);
    }

    public override bool Equals(object? obj) => Equals(obj as ArchitectureInventory);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var finding in Findings)
        {
            hash.Add(finding);
        }

        hash.Add(TotalTopLevelTypeCount);
        hash.Add(PublicTopLevelTypeCount);
        hash.Add(InternalTopLevelTypeCount);
        return hash.ToHashCode();
    }
}
