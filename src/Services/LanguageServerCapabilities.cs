using System;
using System.Collections.Generic;

namespace Zaide.Services;

/// <summary>
/// Server capabilities negotiated at LSP <c>initialize</c> for Phase 10 features.
/// </summary>
public sealed record LanguageServerCapabilities(
    bool CompletionSupported,
    IReadOnlyList<char> CompletionTriggerCharacters,
    bool HoverSupported,
    bool DefinitionSupported,
    bool DocumentSymbolSupported,
    bool WorkspaceSymbolSupported)
{
    /// <summary>No language features supported.</summary>
    public static LanguageServerCapabilities None { get; } = new(
        false,
        Array.Empty<char>(),
        false,
        false,
        false,
        false);
}
