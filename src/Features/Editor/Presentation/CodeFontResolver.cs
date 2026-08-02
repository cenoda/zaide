using System;
using System.Collections.Generic;
using Avalonia.Media;

namespace Zaide.Features.Editor.Presentation;

/// <summary>
/// Resolves the live code-editor <see cref="FontFamily"/> to a monospaced face.
/// Proportional or unloadable code fonts (for example legacy bitmap faces selected
/// from the system font list) make AvaloniaEdit caret/scroll layout pathologically
/// slow on large files; markdown uses the separate prose font and is unaffected.
/// </summary>
internal static class CodeFontResolver
{
    /// <summary>Generic monospaced family used when no stack entry is fixed-pitch.</summary>
    public const string MonospaceFallback = "monospace";

    /// <summary>
    /// Picks the first fixed-pitch family in <paramref name="codeFontFamilySetting"/>
    /// (comma-separated CSS-style stack). Falls back to <see cref="MonospaceFallback"/>.
    /// </summary>
    public static FontFamily Resolve(string codeFontFamilySetting) =>
        Resolve(codeFontFamilySetting, IsFixedPitchFamily);

    /// <summary>
    /// Test seam: same stack walk with an injectable fixed-pitch probe.
    /// </summary>
    internal static FontFamily Resolve(
        string codeFontFamilySetting,
        Func<string, bool> isFixedPitchFamily)
    {
        ArgumentNullException.ThrowIfNull(isFixedPitchFamily);

        foreach (var family in EnumerateStack(codeFontFamilySetting))
        {
            if (isFixedPitchFamily(family))
                return new FontFamily(family);
        }

        return new FontFamily(MonospaceFallback);
    }

    /// <summary>
    /// Yields non-empty comma-separated family names from a CSS-style font stack.
    /// </summary>
    internal static IReadOnlyList<string> EnumerateStack(string? codeFontFamilySetting)
    {
        if (string.IsNullOrWhiteSpace(codeFontFamilySetting))
            return Array.Empty<string>();

        var parts = codeFontFamilySetting.Split(',');
        var families = new List<string>(parts.Length);
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0)
                families.Add(trimmed);
        }

        return families;
    }

    /// <summary>
    /// True when Avalonia can load the family as a fixed-pitch face.
    /// The generic <c>monospace</c> family is always accepted.
    /// </summary>
    internal static bool IsFixedPitchFamily(string familyName)
    {
        if (string.IsNullOrWhiteSpace(familyName))
            return false;

        if (familyName.Equals(MonospaceFallback, StringComparison.OrdinalIgnoreCase))
            return true;

        try
        {
            var typeface = new Typeface(new FontFamily(familyName));
            if (!FontManager.Current.TryGetGlyphTypeface(typeface, out var glyphTypeface))
                return false;

            return glyphTypeface.Metrics.IsFixedPitch;
        }
        catch (InvalidOperationException)
        {
            // FontManager not ready (e.g. early test host) — do not accept unknown faces.
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
