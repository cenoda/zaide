using System;
using Zaide.Features.Language.Application;

namespace Zaide.Features.Language.Contracts;

/// <summary>
/// UI-independent bridge that synchronizes open <see cref="Models.Document"/> instances
/// with the live language-server session via LSP didOpen/didChange/didClose.
/// </summary>
public interface ILanguageDocumentBridge : IDisposable
{
    /// <summary>
    /// Returns true when <paramref name="documentUri"/> is open for the current
    /// sync generation and has successfully sent didOpen for that generation.
    /// </summary>
    bool TryGetOpenDocument(string documentUri, out LanguageTrackedDocumentInfo info);
}
