namespace Zaide.Services;

/// <summary>
/// Zero-based LSP range in the locked utf-16 position encoding.
/// </summary>
/// <param name="StartLine">Zero-based start line.</param>
/// <param name="StartCharacter">Zero-based start character (UTF-16 code units).</param>
/// <param name="EndLine">Zero-based end line.</param>
/// <param name="EndCharacter">Zero-based end character (UTF-16 code units).</param>
public readonly record struct LspRange(
    int StartLine,
    int StartCharacter,
    int EndLine,
    int EndCharacter);
