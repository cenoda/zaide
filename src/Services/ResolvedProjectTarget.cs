namespace Zaide.Services;

/// <summary>
/// A resolved build/run/test target derived from the authoritative project context.
/// </summary>
/// <param name="FilePath">
/// Absolute, normalized path to the selected project or solution file.
/// </param>
/// <param name="WorkingDirectory">
/// Parent directory of <see cref="FilePath"/> used as the process working directory.
/// </param>
/// <param name="Kind">The selected candidate kind.</param>
/// <param name="DisplayName">Presentation-only display name.</param>
public sealed record ResolvedProjectTarget(
    string FilePath,
    string WorkingDirectory,
    ProjectKind Kind,
    string DisplayName);
