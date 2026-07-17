using System;

namespace Zaide.Tests.Architecture;

/// <summary>
/// Stable inventory entry for later exact allowlists (M3+). M2 only produces
/// findings; it does not enforce allowlists or ratchets.
/// </summary>
public sealed class ArchitectureFinding : IEquatable<ArchitectureFinding>
{
    public ArchitectureFinding(
        string kind,
        string stableKey,
        string? relativePath = null,
        int? line = null,
        string? sourceSymbol = null,
        string? targetSymbol = null,
        string? evidence = null)
    {
        Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        StableKey = stableKey ?? throw new ArgumentNullException(nameof(stableKey));
        RelativePath = relativePath;
        Line = line;
        SourceSymbol = sourceSymbol;
        TargetSymbol = targetSymbol;
        Evidence = evidence;
    }

    /// <summary>Finding category, e.g. <c>ProductionType</c>, <c>ProviderResolution</c>.</summary>
    public string Kind { get; }

    /// <summary>
    /// Deterministic, path-normalized key suitable for exact allowlist matching.
    /// </summary>
    public string StableKey { get; }

    public string? RelativePath { get; }

    public int? Line { get; }

    public string? SourceSymbol { get; }

    public string? TargetSymbol { get; }

    public string? Evidence { get; }

    public bool Equals(ArchitectureFinding? other)
    {
        if (other is null)
        {
            return false;
        }

        return Kind == other.Kind
            && StableKey == other.StableKey
            && RelativePath == other.RelativePath
            && Line == other.Line
            && SourceSymbol == other.SourceSymbol
            && TargetSymbol == other.TargetSymbol
            && Evidence == other.Evidence;
    }

    public override bool Equals(object? obj) => Equals(obj as ArchitectureFinding);

    public override int GetHashCode() =>
        HashCode.Combine(Kind, StableKey, RelativePath, Line, SourceSymbol, TargetSymbol, Evidence);

    public override string ToString() => StableKey;
}
