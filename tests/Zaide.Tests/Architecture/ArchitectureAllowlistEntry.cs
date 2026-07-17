using System;

namespace Zaide.Tests.Architecture;

/// <summary>
/// One approved legacy architecture violation. Entries are exact, reviewable,
/// and matched by <see cref="MatchKey"/> (not by free-form wildcards).
/// </summary>
public sealed class ArchitectureAllowlistEntry : IEquatable<ArchitectureAllowlistEntry>
{
    public ArchitectureAllowlistEntry(
        string findingId,
        string category,
        string matchKey,
        string m0FindingId,
        string owner,
        string disposition,
        string rationale,
        string removalBoundary)
    {
        FindingId = findingId ?? throw new ArgumentNullException(nameof(findingId));
        Category = category ?? throw new ArgumentNullException(nameof(category));
        MatchKey = matchKey ?? throw new ArgumentNullException(nameof(matchKey));
        M0FindingId = m0FindingId ?? throw new ArgumentNullException(nameof(m0FindingId));
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Disposition = disposition ?? throw new ArgumentNullException(nameof(disposition));
        Rationale = rationale ?? throw new ArgumentNullException(nameof(rationale));
        RemovalBoundary = removalBoundary ?? throw new ArgumentNullException(nameof(removalBoundary));
    }

    /// <summary>Stable allowlist identity, e.g. <c>R61-AL-NS-SourceControlState</c>.</summary>
    public string FindingId { get; }

    /// <summary>
    /// One of <see cref="ArchitectureRatchet.CategoryNamespaceDirection"/>,
    /// <see cref="ArchitectureRatchet.CategoryLocatorSite"/>, or
    /// <see cref="ArchitectureRatchet.CategoryRootFolderAdmission"/>.
    /// </summary>
    public string Category { get; }

    /// <summary>
    /// Deterministic match key produced by <see cref="ArchitectureRatchet"/> for live findings.
    /// </summary>
    public string MatchKey { get; }

    /// <summary>M0 disposition ID such as <c>R61-V07</c>, or an explicit deferred exception ID.</summary>
    public string M0FindingId { get; }

    /// <summary>Owning feature or composition surface responsible for removal.</summary>
    public string Owner { get; }

    /// <summary>
    /// Disposition classification: movement, dependency inversion, or deferred exception.
    /// </summary>
    public string Disposition { get; }

    /// <summary>Why this exact site is accepted as legacy debt.</summary>
    public string Rationale { get; }

    /// <summary>Named removal boundary (e.g. Refactor 6.3) that must clear this entry.</summary>
    public string RemovalBoundary { get; }

    public bool Equals(ArchitectureAllowlistEntry? other)
    {
        if (other is null)
        {
            return false;
        }

        return FindingId == other.FindingId
            && Category == other.Category
            && MatchKey == other.MatchKey
            && M0FindingId == other.M0FindingId
            && Owner == other.Owner
            && Disposition == other.Disposition
            && Rationale == other.Rationale
            && RemovalBoundary == other.RemovalBoundary;
    }

    public override bool Equals(object? obj) => Equals(obj as ArchitectureAllowlistEntry);

    public override int GetHashCode() =>
        HashCode.Combine(
            FindingId,
            Category,
            MatchKey,
            M0FindingId,
            Owner,
            Disposition,
            Rationale,
            RemovalBoundary);

    public override string ToString() => $"{FindingId} ({MatchKey})";
}
