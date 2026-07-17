using System.Collections.Generic;
using Zaide.Features.Language.Application;

namespace Zaide.Services;

/// <summary>Parsed document or workspace symbol response.</summary>
public sealed record LanguageServerSymbolResult(IReadOnlyList<LanguageSymbol> Symbols);
