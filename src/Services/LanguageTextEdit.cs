namespace Zaide.Services;

/// <summary>
/// One LSP <c>TextEdit</c>: replace <see cref="Range"/> with <see cref="NewText"/>.
/// </summary>
/// <param name="Range">Zero-based utf-16 range in the source document.</param>
/// <param name="NewText">Replacement text (may be empty for a pure delete).</param>
public sealed record LanguageTextEdit(LanguageDiagnosticRange Range, string NewText);
