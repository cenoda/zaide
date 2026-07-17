using System.Collections.Generic;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Language.Application;

namespace Zaide.Features.Language.Infrastructure.Lsp;

/// <summary>
/// Raw <c>textDocument/publishDiagnostics</c> payload from a live session.
/// Not yet validated against generation/version/document open state.
/// </summary>
/// <param name="Generation">Session generation that received the notification.</param>
/// <param name="DocumentUri">Document URI as reported by the server (may need normalization).</param>
/// <param name="Version">Optional document version from the server.</param>
/// <param name="Diagnostics">Raw diagnostic entries from the server.</param>
public sealed record LanguageServerPublishDiagnostics(
    long Generation,
    string DocumentUri,
    int? Version,
    IReadOnlyList<LanguageServerDiagnosticPayload> Diagnostics);

/// <summary>
/// One unvalidated diagnostic payload entry from the language server.
/// </summary>
public sealed record LanguageServerDiagnosticPayload(
    LanguageDiagnosticSeverity Severity,
    string Message,
    string? Code,
    string? Source,
    LspRange Range);
