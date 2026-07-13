using System.Collections.Generic;

namespace Zaide.Services;

/// <summary>Parsed document or workspace symbol response.</summary>
public sealed record LanguageServerSymbolResult(IReadOnlyList<LanguageSymbol> Symbols);
