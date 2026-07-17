using System.Collections.Generic;
using Zaide.Features.Language.Application;

namespace Zaide.Services;

/// <summary>Parsed <c>textDocument/definition</c> response locations.</summary>
public sealed record LanguageServerDefinitionResult(IReadOnlyList<LanguageLocation> Locations);
