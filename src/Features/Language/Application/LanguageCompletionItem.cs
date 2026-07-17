namespace Zaide.Features.Language.Application;

/// <summary>
/// One completion item validated for the active document version.
/// Offsets are document offsets in UTF-16 code units.
/// </summary>
public sealed record LanguageCompletionItem(
    string Label,
    string InsertText,
    int ReplaceStartOffset,
    int ReplaceLength,
    string? Detail,
    string? SortText);
