namespace Zaide.Features.Language.Infrastructure.Lsp;

/// <summary>Raw hover content returned from <c>textDocument/hover</c>.</summary>
public sealed record LanguageServerHoverResult(string? Content);
