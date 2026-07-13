using System;
using System.Collections.Generic;

namespace Zaide.Services;

/// <summary>
/// Server capabilities negotiated at LSP <c>initialize</c> for Phase 10 M4 features.
/// </summary>
public sealed record LanguageServerCapabilities(
    bool CompletionSupported,
    IReadOnlyList<char> CompletionTriggerCharacters,
    bool HoverSupported)
{
    /// <summary>No completion or hover support.</summary>
    public static LanguageServerCapabilities None { get; } = new(false, Array.Empty<char>(), false);
}
