using System;
using System.IO;

namespace Zaide.Services;

/// <summary>
/// A supported project or solution file discovered at the workspace root.
/// Identity is ordinal equality on a normalised, absolute <see cref="FilePath"/>.
/// </summary>
/// <param name="FilePath">
/// Normalised, absolute path to the project/solution file.
/// Used for candidate identity and ordinal sorting.
/// </param>
/// <param name="DisplayName">
/// File name without extension, preserving original casing.
/// Presentation-only; never used for identity or ordering.
/// Derived as <c>Path.GetFileNameWithoutExtension(FilePath)</c>.
/// </param>
/// <param name="Kind">
/// The project kind derived from the file extension:
/// <c>.sln</c> → <see cref="ProjectKind.Solution"/>,
/// <c>.slnx</c> → <see cref="ProjectKind.SolutionX"/>,
/// <c>.csproj</c> → <see cref="ProjectKind.CSharpProject"/>.
/// </param>
public sealed record ProjectCandidate(
    string FilePath,
    string DisplayName,
    ProjectKind Kind)
{
    public override string ToString() => DisplayName;
}
