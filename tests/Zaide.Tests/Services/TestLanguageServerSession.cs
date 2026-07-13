using System;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Services;

namespace Zaide.Tests.Services;

/// <summary>
/// Default no-op language-request stubs for <see cref="ILanguageServerSession"/> test fakes.
/// </summary>
internal static class TestLanguageServerSession
{
    public static LanguageServerCapabilities DefaultCapabilities { get; } = new(
        CompletionSupported: true,
        CompletionTriggerCharacters: new[] { '.', '\'' },
        HoverSupported: true,
        DefinitionSupported: true,
        DocumentSymbolSupported: true,
        WorkspaceSymbolSupported: true);

    public static Task<LanguageServerCompletionResult?> EmptyCompletionAsync(
        string documentUri,
        int line,
        int character,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<LanguageServerCompletionResult?>(
            new LanguageServerCompletionResult(Array.Empty<LanguageServerCompletionItem>()));

    public static Task<LanguageServerHoverResult?> EmptyHoverAsync(
        string documentUri,
        int line,
        int character,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<LanguageServerHoverResult?>(new LanguageServerHoverResult(null));

    public static Task<LanguageServerDefinitionResult?> EmptyDefinitionAsync(
        string documentUri,
        int line,
        int character,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<LanguageServerDefinitionResult?>(
            new LanguageServerDefinitionResult(Array.Empty<LanguageLocation>()));

    public static Task<LanguageServerSymbolResult?> EmptySymbolsAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<LanguageServerSymbolResult?>(
            new LanguageServerSymbolResult(Array.Empty<LanguageSymbol>()));
}
