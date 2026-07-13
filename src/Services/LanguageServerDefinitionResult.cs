using System.Collections.Generic;

namespace Zaide.Services;

/// <summary>Parsed <c>textDocument/definition</c> response locations.</summary>
public sealed record LanguageServerDefinitionResult(IReadOnlyList<LanguageLocation> Locations);
