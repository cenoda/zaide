using System;

namespace Zaide.Services;

/// <summary>
/// UI-independent bridge that synchronizes open <see cref="Models.Document"/> instances
/// with the live language-server session via LSP didOpen/didChange/didClose.
/// </summary>
public interface ILanguageDocumentBridge : IDisposable
{
}
