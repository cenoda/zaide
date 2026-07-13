using System;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Services;

namespace Zaide.Tests.Services;

/// <summary>
/// Default no-op completion/hover stubs for <see cref="ILanguageServerSession"/> test fakes.
/// </summary>
internal static class TestLanguageServerSession
{
    public static LanguageServerCapabilities DefaultCapabilities { get; } = new(
        CompletionSupported: true,
        CompletionTriggerCharacters: new[] { '.', '\'' },
        HoverSupported: true);

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
}
