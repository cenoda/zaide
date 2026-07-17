using Zaide.Services;
using Zaide.Features.Language.Infrastructure.Lsp;

namespace Zaide.Features.Language.Application;

/// <summary>
/// One LSP <c>TextEdit</c>: replace <see cref="Range"/> with <see cref="NewText"/>.
/// </summary>
/// <param name="Range">Zero-based utf-16 range in the source document.</param>
/// <param name="NewText">Replacement text (may be empty for a pure delete).</param>
public sealed record LanguageTextEdit(LspRange Range, string NewText);
