using Avalonia.Styling;
using TextMateSharp.Grammars;
using Xunit;
using Zaide.Features.Editor.Presentation;

namespace Zaide.Tests.Features.Editor.Presentation;

/// <summary>
/// Tests for TextMate theme selection aligned with the app theme variant.
/// </summary>
public class EditorViewTextMateThemeTests
{
    [Theory]
    [InlineData("Light", ThemeName.LightPlus)]
    [InlineData("Dark", ThemeName.DarkPlus)]
    public void GetTextMateThemeName_MapsAppVariantToBundledTheme(string variantKey, ThemeName expected)
    {
        var variant = variantKey == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        Assert.Equal(expected, EditorView.GetTextMateThemeName(variant));
    }

    [Fact]
    public void GetTextMateThemeName_DefaultVariant_UsesLightPlus()
    {
        Assert.Equal(ThemeName.LightPlus, EditorView.GetTextMateThemeName(ThemeVariant.Default));
    }
}
