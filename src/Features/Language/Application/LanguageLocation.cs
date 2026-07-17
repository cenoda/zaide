using Zaide.App.Composition;
using Zaide.Features.Language.Infrastructure.Lsp;

namespace Zaide.Features.Language.Application;

/// <summary>
/// One validated language-navigation target (definition or symbol location).
/// Presentation labels are derived separately; this is structured data only.
/// </summary>
/// <param name="DocumentUri">Normalized absolute <c>file://</c> URI.</param>
/// <param name="FilePath">Absolute on-disk path when the URI is a local file; otherwise null.</param>
/// <param name="Range">Zero-based LSP range in utf-16 units (selection/target range).</param>
/// <param name="ContainerName">Optional container (class/namespace) for ordering/display keys.</param>
/// <param name="Name">Optional symbol name for ordering/display keys.</param>
public sealed record LanguageLocation(
    string DocumentUri,
    string? FilePath,
    LspRange Range,
    string? ContainerName,
    string? Name);
