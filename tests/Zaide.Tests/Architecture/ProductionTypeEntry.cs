using System;

namespace Zaide.Tests.Architecture;

/// <summary>
/// Compiled top-level production type visibility entry (non-nested, non-compiler-generated).
/// </summary>
public sealed class ProductionTypeEntry : IEquatable<ProductionTypeEntry>
{
    public ProductionTypeEntry(string fullName, string @namespace, bool isPublic)
    {
        FullName = fullName ?? throw new ArgumentNullException(nameof(fullName));
        Namespace = @namespace ?? throw new ArgumentNullException(nameof(@namespace));
        IsPublic = isPublic;
    }

    /// <summary>Type full name as reported by reflection.</summary>
    public string FullName { get; }

    public string Namespace { get; }

    public bool IsPublic { get; }

    public string Visibility => IsPublic ? "public" : "internal";

    public bool Equals(ProductionTypeEntry? other)
    {
        if (other is null)
        {
            return false;
        }

        return FullName == other.FullName
            && Namespace == other.Namespace
            && IsPublic == other.IsPublic;
    }

    public override bool Equals(object? obj) => Equals(obj as ProductionTypeEntry);

    public override int GetHashCode() => HashCode.Combine(FullName, Namespace, IsPublic);
}
