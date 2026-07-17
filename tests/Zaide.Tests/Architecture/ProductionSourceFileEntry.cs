using System;

namespace Zaide.Tests.Architecture;

/// <summary>
/// Tracked production C# source file with folder and declared-namespace placement.
/// </summary>
public sealed class ProductionSourceFileEntry : IEquatable<ProductionSourceFileEntry>
{
    public ProductionSourceFileEntry(
        string relativePath,
        string technicalFolder,
        string declaredNamespace)
    {
        RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
        TechnicalFolder = technicalFolder ?? throw new ArgumentNullException(nameof(technicalFolder));
        DeclaredNamespace = declaredNamespace ?? throw new ArgumentNullException(nameof(declaredNamespace));
    }

    /// <summary>Repo-relative path using forward slashes, e.g. <c>src/Services/Foo.cs</c>.</summary>
    public string RelativePath { get; }

    /// <summary>
    /// Technical folder key: <c>src</c> for root composition files, otherwise
    /// First path segment under <c>src/</c> (e.g. <c>Models</c>, <c>Services</c>,
    /// <c>UI</c>, <c>ViewModels</c>, <c>Views</c>), or <c>src</c> for root composition files.
    /// </summary>
    public string TechnicalFolder { get; }

    /// <summary>First declared C# namespace in the file.</summary>
    public string DeclaredNamespace { get; }

    public bool Equals(ProductionSourceFileEntry? other)
    {
        if (other is null)
        {
            return false;
        }

        return RelativePath == other.RelativePath
            && TechnicalFolder == other.TechnicalFolder
            && DeclaredNamespace == other.DeclaredNamespace;
    }

    public override bool Equals(object? obj) => Equals(obj as ProductionSourceFileEntry);

    public override int GetHashCode() =>
        HashCode.Combine(RelativePath, TechnicalFolder, DeclaredNamespace);
}
