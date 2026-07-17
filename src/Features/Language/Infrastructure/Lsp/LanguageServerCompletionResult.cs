using System.Collections.Generic;

namespace Zaide.Features.Language.Infrastructure.Lsp;

/// <summary>Raw completion items returned from <c>textDocument/completion</c>.</summary>
public sealed record LanguageServerCompletionResult(
    IReadOnlyList<LanguageServerCompletionItem> Items);

/// <summary>One raw completion item before document offset mapping.</summary>
public sealed record LanguageServerCompletionItem(
    string Label,
    string? InsertText,
    string? Detail,
    string? SortText,
    LspRange? TextEditRange,
    string? TextEditNewText);
