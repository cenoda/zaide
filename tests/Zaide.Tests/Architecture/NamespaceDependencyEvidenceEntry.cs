using System;

namespace Zaide.Tests.Architecture;

/// <summary>
/// Source-level technical-namespace dependency evidence used for later
/// forbidden-edge allowlists. Inventory only in M2.
/// </summary>
public sealed class NamespaceDependencyEvidenceEntry : IEquatable<NamespaceDependencyEvidenceEntry>
{
    public NamespaceDependencyEvidenceEntry(
        string relativePath,
        int line,
        string sourceTechnicalFolder,
        string targetNamespaceFragment,
        string matchedText)
    {
        RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
        Line = line;
        SourceTechnicalFolder = sourceTechnicalFolder
            ?? throw new ArgumentNullException(nameof(sourceTechnicalFolder));
        TargetNamespaceFragment = targetNamespaceFragment
            ?? throw new ArgumentNullException(nameof(targetNamespaceFragment));
        MatchedText = matchedText ?? throw new ArgumentNullException(nameof(matchedText));
    }

    public string RelativePath { get; }

    public int Line { get; }

    public string SourceTechnicalFolder { get; }

    public string TargetNamespaceFragment { get; }

    public string MatchedText { get; }

    public bool Equals(NamespaceDependencyEvidenceEntry? other)
    {
        if (other is null)
        {
            return false;
        }

        return RelativePath == other.RelativePath
            && Line == other.Line
            && SourceTechnicalFolder == other.SourceTechnicalFolder
            && TargetNamespaceFragment == other.TargetNamespaceFragment
            && MatchedText == other.MatchedText;
    }

    public override bool Equals(object? obj) => Equals(obj as NamespaceDependencyEvidenceEntry);

    public override int GetHashCode() =>
        HashCode.Combine(
            RelativePath,
            Line,
            SourceTechnicalFolder,
            TargetNamespaceFragment,
            MatchedText);
}
