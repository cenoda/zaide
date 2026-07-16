using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;

namespace Zaide.Views;

/// <summary>
/// Discovers installed fonts through Avalonia's <see cref="FontManager"/> and
/// builds picker rows with safe preview fallbacks.
/// </summary>
public static class InstalledFontCatalog
{
    private static IReadOnlyList<string>? _cachedInstalled;

    private static readonly string[] FallbackFamilyNames =
    [
        "Cascadia Code",
        "Consolas",
        "DejaVu Sans Mono",
        "Fira Code",
        "Georgia",
        "JetBrains Mono",
        "Liberation Mono",
        "Liberation Sans",
        "Liberation Serif",
        "monospace",
        "Noto Sans",
        "Noto Serif",
        "Roboto",
        "sans-serif",
        "serif",
        "Ubuntu",
        "Ubuntu Mono",
    ];
    /// <summary>
    /// Returns installed fonts (sorted) plus the current primary family when it
    /// is not installed so persisted values remain selectable.
    /// </summary>
    public static IReadOnlyList<FontPickerEntry> BuildEntries(string? currentFamilySetting)
    {
        var primary = ExtractPrimaryFamilyName(currentFamilySetting);
        var installed = GetInstalledFamilyNames();
        var entries = new List<FontPickerEntry>(installed.Count + 1);

        if (!string.IsNullOrWhiteSpace(primary)
            && !installed.Contains(primary, StringComparer.OrdinalIgnoreCase))
        {
            entries.Add(FontPickerEntry.Unavailable(primary));
        }

        foreach (var name in installed)
            entries.Add(FontPickerEntry.Available(name));

        return entries;
    }

    /// <summary>
    /// Extracts the first comma-separated family from a CSS-style font stack.
    /// </summary>
    public static string ExtractPrimaryFamilyName(string? familySetting)
    {
        if (string.IsNullOrWhiteSpace(familySetting))
            return string.Empty;

        var comma = familySetting.IndexOf(',');
        return (comma >= 0 ? familySetting[..comma] : familySetting).Trim();
    }

    /// <summary>
    /// Resolves a preview <see cref="FontFamily"/> for a picker row, falling
    /// back to the platform default when the family cannot be loaded.
    /// </summary>
    public static FontFamily ResolvePreviewFontFamily(string familyName, bool isAvailable)
    {
        if (!isAvailable || string.IsNullOrWhiteSpace(familyName))
            return ResolveSafeFallbackFontFamily();

        if (TryResolveInstalledPreviewFontFamily(familyName, out var preview))
            return preview;

        try
        {
            return new FontFamily(familyName);
        }
        catch (ArgumentException)
        {
            return ResolveSafeFallbackFontFamily();
        }
    }

    private static bool TryResolveInstalledPreviewFontFamily(string familyName, out FontFamily preview)
    {
        preview = ResolveSafeFallbackFontFamily();
        try
        {
            var candidate = new FontFamily(familyName);
            var typeface = new Typeface(candidate);
            if (FontManager.Current.TryGetGlyphTypeface(typeface, out _))
            {
                preview = candidate;
                return true;
            }
        }
        catch (InvalidOperationException)
        {
            preview = new FontFamily(familyName);
            return true;
        }
        catch (ArgumentException)
        {
        }

        return false;
    }

    private static FontFamily ResolveSafeFallbackFontFamily()
    {
        try
        {
            return FontManager.Current.DefaultFontFamily;
        }
        catch (InvalidOperationException)
        {
            return new FontFamily("sans-serif");
        }
    }

    private static IReadOnlyList<string> GetInstalledFamilyNames()
    {
        if (_cachedInstalled is not null)
            return _cachedInstalled;

        try
        {
            _cachedInstalled = FontManager.Current.SystemFonts
                .Select(fontFamily => fontFamily.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (InvalidOperationException)
        {
            _cachedInstalled = FallbackFamilyNames
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return _cachedInstalled;
    }
}
