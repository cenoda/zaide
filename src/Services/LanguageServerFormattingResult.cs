using System;
using System.Collections.Generic;
using Zaide.Features.Language.Application;

namespace Zaide.Services;

/// <summary>
/// Parsed result of <c>textDocument/formatting</c>.
/// </summary>
/// <param name="Edits">Ordered list of text edits as returned by the server.</param>
public sealed record LanguageServerFormattingResult(IReadOnlyList<LanguageTextEdit> Edits)
{
    /// <summary>Empty successful formatting response (no edits).</summary>
    public static LanguageServerFormattingResult Empty { get; } =
        new(Array.Empty<LanguageTextEdit>());
}
