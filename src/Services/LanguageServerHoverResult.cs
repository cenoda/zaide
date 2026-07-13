namespace Zaide.Services;

/// <summary>Raw hover content returned from <c>textDocument/hover</c>.</summary>
public sealed record LanguageServerHoverResult(string? Content);
