using System;
using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Services;

/// <summary>
/// A live language-server child process with an initialized StreamJsonRpc transport.
/// </summary>
public interface ILanguageServerSession : IAsyncDisposable
{
    /// <summary>Generation captured when this session was created.</summary>
    long Generation { get; }

    /// <summary>Child process id, or <c>null</c> when not launched.</summary>
    int? ProcessId { get; }

    /// <summary>Whether the child process has exited.</summary>
    bool HasExited { get; }

    /// <summary>
    /// Raised once when the child process exits. The argument is <see cref="Generation"/>.
    /// </summary>
    event Action<long>? ProcessExited;

    /// <summary>
    /// Raised when the server sends <c>textDocument/publishDiagnostics</c>.
    /// Handlers must not throw; generation is the session that received the notification.
    /// </summary>
    event Action<LanguageServerPublishDiagnostics>? DiagnosticsPublished;

    /// <summary>Graceful LSP <c>shutdown</c> followed by <c>exit</c>.</summary>
    Task ShutdownAsync(CancellationToken cancellationToken);

    /// <summary>Force-kill the process tree without protocol shutdown.</summary>
    Task ForceKillAsync();

    /// <summary>Sends <c>textDocument/didOpen</c> for one C# document.</summary>
    Task NotifyDidOpenAsync(
        string documentUri,
        int version,
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>Sends a full-document <c>textDocument/didChange</c>.</summary>
    Task NotifyDidChangeAsync(
        string documentUri,
        int version,
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>Sends <c>textDocument/didClose</c>.</summary>
    Task NotifyDidCloseAsync(
        string documentUri,
        CancellationToken cancellationToken = default);

    /// <summary>Capabilities negotiated during <c>initialize</c>.</summary>
    LanguageServerCapabilities Capabilities { get; }

    /// <summary>Issues <c>textDocument/completion</c> at a zero-based utf-16 position.</summary>
    Task<LanguageServerCompletionResult?> RequestCompletionAsync(
        string documentUri,
        int line,
        int character,
        CancellationToken cancellationToken = default);

    /// <summary>Issues <c>textDocument/hover</c> at a zero-based utf-16 position.</summary>
    Task<LanguageServerHoverResult?> RequestHoverAsync(
        string documentUri,
        int line,
        int character,
        CancellationToken cancellationToken = default);

    /// <summary>Issues <c>textDocument/definition</c> at a zero-based utf-16 position.</summary>
    Task<LanguageServerDefinitionResult?> RequestDefinitionAsync(
        string documentUri,
        int line,
        int character,
        CancellationToken cancellationToken = default);

    /// <summary>Issues <c>textDocument/documentSymbol</c> for an open document.</summary>
    Task<LanguageServerSymbolResult?> RequestDocumentSymbolsAsync(
        string documentUri,
        CancellationToken cancellationToken = default);

    /// <summary>Issues <c>workspace/symbol</c> for a query string.</summary>
    Task<LanguageServerSymbolResult?> RequestWorkspaceSymbolsAsync(
        string query,
        CancellationToken cancellationToken = default);
}
