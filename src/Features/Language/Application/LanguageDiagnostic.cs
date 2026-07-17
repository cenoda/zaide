using Zaide.Services;
using Zaide.Features.Language.Infrastructure.Lsp;

namespace Zaide.Features.Language.Application;

/// <summary>
/// One structured diagnostic associated with a document version and session generation.
/// UI layers project this data; formatted strings are not the source of truth.
/// </summary>
/// <param name="DocumentUri">Normalized absolute <c>file://</c> URI.</param>
/// <param name="FilePath">Absolute on-disk path for the document.</param>
/// <param name="DocumentVersion">Tracked LSP document version this diagnostic applies to.</param>
/// <param name="SessionGeneration">Language-session generation that published this diagnostic.</param>
/// <param name="Severity">Structured severity.</param>
/// <param name="Message">Server diagnostic message text.</param>
/// <param name="Code">Optional diagnostic code (e.g. CS1002).</param>
/// <param name="Source">Optional diagnostic source (e.g. csharp-ls).</param>
/// <param name="Range">Zero-based LSP range in utf-16 units.</param>
/// <param name="StartOffset">Inclusive document offset in UTF-16 code units.</param>
/// <param name="EndOffset">Exclusive document offset in UTF-16 code units.</param>
public sealed record LanguageDiagnostic(
    string DocumentUri,
    string FilePath,
    int DocumentVersion,
    long SessionGeneration,
    LanguageDiagnosticSeverity Severity,
    string Message,
    string? Code,
    string? Source,
    LspRange Range,
    int StartOffset,
    int EndOffset);
