using System;
using System.Collections.Generic;

namespace Zaide.Features.Language.Infrastructure.Lsp;

/// <summary>
/// Server capabilities negotiated at LSP <c>initialize</c> for Phase 10 features.
/// </summary>
public sealed record LanguageServerCapabilities(
    bool CompletionSupported,
    IReadOnlyList<char> CompletionTriggerCharacters,
    bool HoverSupported,
    bool DefinitionSupported,
    bool DocumentSymbolSupported,
    bool WorkspaceSymbolSupported,
    bool DocumentFormattingSupported = false)
{
    /// <summary>No language features supported.</summary>
    public static LanguageServerCapabilities None { get; } = new(
        false,
        Array.Empty<char>(),
        false,
        false,
        false,
        false,
        false);
}
