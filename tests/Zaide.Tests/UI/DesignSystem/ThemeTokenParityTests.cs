using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Xunit;
using Zaide.Tests.Infrastructure;
using Zaide.UI.DesignSystem;

namespace Zaide.Tests.UI.DesignSystem;

[Collection("AvaloniaUiInitialization")]
public class ThemeTokenParityTests
{
    [Fact]
    public void LightAndDarkExposeIdenticalKeySets()
    {
        var app = ReactiveUiTestBootstrap.EnsureApplication();
        var lightKeys = GetDictionaryKeys(GetThemeDictionary(app, ThemeVariant.Light));
        var darkKeys = GetDictionaryKeys(GetThemeDictionary(app, ThemeVariant.Dark));

        Assert.Equal(lightKeys, darkKeys);
    }

    [Fact]
    public void LightRamp_MeetsTextAndBorderContrastMinimums()
    {
        var app = ReactiveUiTestBootstrap.EnsureApplication();
        AssertContrastMinimums(GetThemeDictionary(app, ThemeVariant.Light), "Light");
    }

    [Fact]
    public void DarkRamp_MeetsTextContrastMinimums()
    {
        var app = ReactiveUiTestBootstrap.EnsureApplication();
        AssertTextContrastMinimums(GetThemeDictionary(app, ThemeVariant.Dark), "Dark");
    }

    [Fact]
    public void DarkThemeDictionary_ExposesNavyPaletteValues()
    {
        var app = ReactiveUiTestBootstrap.EnsureApplication();
        var dark = GetThemeDictionary(app, ThemeVariant.Dark);
        Assert.Equal(Color.Parse("#066ADB"), GetColor(dark, "PrimaryAccentBrushColor"));
        Assert.Equal(Color.Parse("#E3E4F4"), GetColor(dark, "TextPrimaryBrushColor"));
    }

    private static ResourceDictionary GetThemeDictionary(Application app, ThemeVariant variant)
    {
        if (app.Resources is not ResourceDictionary root)
        {
            throw new InvalidOperationException("Application resources are not a resource dictionary.");
        }

        if (!root.ThemeDictionaries.TryGetValue(variant, out var themeDictionary) ||
            themeDictionary is not ResourceDictionary resourceDictionary)
        {
            throw new InvalidOperationException($"Theme dictionary '{variant}' was not found.");
        }

        return resourceDictionary;
    }

    private static IReadOnlyList<string> GetDictionaryKeys(ResourceDictionary dictionary) =>
        dictionary.Keys
            .Cast<object>()
            .Select(key => key.ToString() ?? string.Empty)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

    private static void AssertTextContrastMinimums(ResourceDictionary dictionary, string variantName)
    {
        var surface = GetColor(dictionary, "SurfaceBaseBrushColor");
        var textPrimary = GetColor(dictionary, "TextPrimaryBrushColor");
        var textSecondary = GetColor(dictionary, "TextSecondaryBrushColor");

        Assert.True(
            ContrastRatio(textPrimary, surface) >= 4.5,
            $"{variantName} TextPrimary on SurfaceBase must be >= 4.5:1.");
        Assert.True(
            ContrastRatio(textSecondary, surface) >= 3.0,
            $"{variantName} TextSecondary on SurfaceBase must be >= 3:1.");
    }

    private static void AssertContrastMinimums(ResourceDictionary dictionary, string variantName)
    {
        AssertTextContrastMinimums(dictionary, variantName);

        var surface = GetColor(dictionary, "SurfaceBaseBrushColor");
        var separator = GetColor(dictionary, "SeparatorBrushColor");

        Assert.True(
            ContrastRatio(separator, surface) >= 3.0,
            $"{variantName} Separator on SurfaceBase must be >= 3:1.");
    }

    private static Color GetColor(ResourceDictionary dictionary, string key)
    {
        if (!dictionary.TryGetResource(key, theme: null, out var value))
        {
            throw new InvalidOperationException($"Resource '{key}' was not found.");
        }

        return value switch
        {
            Color color => color,
            ISolidColorBrush brush => brush.Color,
            _ => throw new InvalidOperationException($"Resource '{key}' is not a color."),
        };
    }

    private static double ContrastRatio(Color foreground, Color background)
    {
        var foregroundLuminance = RelativeLuminance(foreground);
        var backgroundLuminance = RelativeLuminance(background);
        var lighter = Math.Max(foregroundLuminance, backgroundLuminance);
        var darker = Math.Min(foregroundLuminance, backgroundLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(Color color)
    {
        static double Channel(byte channel)
        {
            var normalized = channel / 255d;
            return normalized <= 0.03928
                ? normalized / 12.92
                : Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(color.R))
            + (0.7152 * Channel(color.G))
            + (0.0722 * Channel(color.B));
    }
}
