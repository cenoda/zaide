using System.Collections.Generic;

namespace Zaide.Features.Language.Application;

/// <summary>
/// One structured document or workspace symbol. Independent of presentation formatting.
/// </summary>
/// <param name="Name">Symbol name.</param>
/// <param name="Kind">LSP <c>SymbolKind</c> integer.</param>
/// <param name="Detail">Optional detail text from the server.</param>
/// <param name="ContainerName">Optional container name (workspace symbols / SymbolInformation).</param>
/// <param name="Location">Navigation target when present and valid.</param>
/// <param name="Children">Hierarchical children (document symbols); empty for flat workspace symbols.</param>
/// <param name="Depth">Flattened presentation depth (0 = top-level).</param>
public sealed record LanguageSymbol(
    string Name,
    int Kind,
    string? Detail,
    string? ContainerName,
    LanguageLocation? Location,
    IReadOnlyList<LanguageSymbol> Children,
    int Depth);
