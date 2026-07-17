using System;

namespace Zaide.Tests.Architecture;

/// <summary>
/// Root-folder admission evidence for a tracked production source path.
/// Records current technical placement; does not enforce target admission rules.
/// </summary>
public sealed class RootFolderAdmissionEvidenceEntry : IEquatable<RootFolderAdmissionEvidenceEntry>
{
    public RootFolderAdmissionEvidenceEntry(
        string relativePath,
        string technicalFolder,
        bool isSrcRootCompositionFile,
        bool isUnderRootInfrastructure,
        bool isUnderUiShared)
    {
        RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
        TechnicalFolder = technicalFolder ?? throw new ArgumentNullException(nameof(technicalFolder));
        IsSrcRootCompositionFile = isSrcRootCompositionFile;
        IsUnderRootInfrastructure = isUnderRootInfrastructure;
        IsUnderUiShared = isUnderUiShared;
    }

    public string RelativePath { get; }

    public string TechnicalFolder { get; }

    /// <summary>True when the file lives directly under <c>src/</c>.</summary>
    public bool IsSrcRootCompositionFile { get; }

    /// <summary>True when the path is under target root <c>src/Infrastructure/</c>.</summary>
    public bool IsUnderRootInfrastructure { get; }

    /// <summary>True when the path is under target root <c>src/UI/Shared/</c>.</summary>
    public bool IsUnderUiShared { get; }

    public bool Equals(RootFolderAdmissionEvidenceEntry? other)
    {
        if (other is null)
        {
            return false;
        }

        return RelativePath == other.RelativePath
            && TechnicalFolder == other.TechnicalFolder
            && IsSrcRootCompositionFile == other.IsSrcRootCompositionFile
            && IsUnderRootInfrastructure == other.IsUnderRootInfrastructure
            && IsUnderUiShared == other.IsUnderUiShared;
    }

    public override bool Equals(object? obj) => Equals(obj as RootFolderAdmissionEvidenceEntry);

    public override int GetHashCode() =>
        HashCode.Combine(
            RelativePath,
            TechnicalFolder,
            IsSrcRootCompositionFile,
            IsUnderRootInfrastructure,
            IsUnderUiShared);
}
