namespace Zaide.Features.Language.Application;

/// <summary>
/// A validated completion commit for the active document.
/// </summary>
public sealed record LanguageCompletionCommit(
    long RequestId,
    long SessionGeneration,
    string FilePath,
    string DocumentUri,
    int DocumentVersion,
    int CaretOffset,
    int ReplaceStartOffset,
    int ReplaceLength,
    string InsertText);
