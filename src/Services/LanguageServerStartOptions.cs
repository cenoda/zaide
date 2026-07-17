using Zaide.Features.ProjectSystem.Domain;

namespace Zaide.Services;

/// <summary>
/// Inputs required to start one csharp-ls session for a single eligible project context.
/// </summary>
/// <param name="Generation">Session generation captured at start time.</param>
/// <param name="ServerPath">Resolved absolute path to the csharp-ls binary.</param>
/// <param name="ProjectFilePath">Absolute winning <see cref="ProjectCandidate.FilePath"/>.</param>
/// <param name="WorkspaceFolderPath">
/// Parent directory of <paramref name="ProjectFilePath"/> used as the sole workspace folder.
/// </param>
/// <param name="ProjectKind">Kind of the winning candidate (controls optional solution hint).</param>
public sealed record LanguageServerStartOptions(
    long Generation,
    string ServerPath,
    string ProjectFilePath,
    string WorkspaceFolderPath,
    ProjectKind ProjectKind);
