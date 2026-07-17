using System;

namespace Zaide.Tests.Architecture;

/// <summary>
/// A live architecture finding that is subject to the M3 allowlist ratchet.
/// </summary>
public sealed class ArchitectureViolation : IEquatable<ArchitectureViolation>
{
    public ArchitectureViolation(
        string category,
        string matchKey,
        string relativePath,
        string summary)
    {
        Category = category ?? throw new ArgumentNullException(nameof(category));
        MatchKey = matchKey ?? throw new ArgumentNullException(nameof(matchKey));
        RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
        Summary = summary ?? throw new ArgumentNullException(nameof(summary));
    }

    public string Category { get; }

    public string MatchKey { get; }

    public string RelativePath { get; }

    public string Summary { get; }

    public bool Equals(ArchitectureViolation? other)
    {
        if (other is null)
        {
            return false;
        }

        return Category == other.Category
            && MatchKey == other.MatchKey
            && RelativePath == other.RelativePath
            && Summary == other.Summary;
    }

    public override bool Equals(object? obj) => Equals(obj as ArchitectureViolation);

    public override int GetHashCode() =>
        HashCode.Combine(Category, MatchKey, RelativePath, Summary);

    public override string ToString() => $"{Category}:{MatchKey}";
}
