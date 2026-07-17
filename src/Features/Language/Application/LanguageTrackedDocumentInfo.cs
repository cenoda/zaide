namespace Zaide.Features.Language.Application;

/// <summary>
/// Open-document tracking info exposed by the document bridge for stale-result checks.
/// </summary>
/// <param name="DocumentUri">Normalized absolute <c>file://</c> URI.</param>
/// <param name="FilePath">Absolute on-disk path.</param>
/// <param name="Version">Current monotonic LSP document version.</param>
/// <param name="Generation">Sync generation for which this document is open.</param>
public readonly record struct LanguageTrackedDocumentInfo(
    string DocumentUri,
    string FilePath,
    int Version,
    long Generation);
