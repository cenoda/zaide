using Avalonia.Media;
using Xunit;
using Zaide.UI.DesignSystem;

namespace Zaide.Tests.UI.DesignSystem;

public class PaletteTokensTests
{
    [Fact]
    public void TextPrimaryBrush_FallsBackToNavyPrimary_WhenNoResource()
    {
        var brush = Assert.IsType<SolidColorBrush>(PaletteTokens.TextPrimaryBrush);
        Assert.Equal(Color.Parse("#E3E4F4"), brush.Color);
    }

    [Fact]
    public void PrimaryAccentBrush_FallsBackToAccent_WhenNoResource()
    {
        var brush = Assert.IsType<SolidColorBrush>(PaletteTokens.PrimaryAccentBrush);
        Assert.Equal(Color.Parse("#066ADB"), brush.Color);
    }

    [Fact]
    public void TextSecondaryBrush_FallsBackToMutedSecondary_WhenNoResource()
    {
        var brush = Assert.IsType<SolidColorBrush>(PaletteTokens.TextSecondaryBrush);
        Assert.Equal(Color.Parse("#8B95A5"), brush.Color);
    }

    [Fact]
    public void SurfaceRaisedColor_FallsBackToRaisedSurface_WhenNoResource()
    {
        Assert.Equal(Color.Parse("#243352"), PaletteTokens.SurfaceRaisedColor);
    }

    [Fact]
    public void PrimaryAccentColor_FallsBackToAccent_WhenNoResource()
    {
        Assert.Equal(Color.Parse("#066ADB"), PaletteTokens.PrimaryAccentColor);
    }

    [Fact]
    public void SuccessBrush_FallsBackToSuccessGreen_WhenNoResource()
    {
        var brush = Assert.IsType<SolidColorBrush>(PaletteTokens.SuccessBrush);
        Assert.Equal(Color.Parse("#28A745"), brush.Color);
    }

    [Fact]
    public void SurfacePanelBrush_FallsBackToPanelSurface_WhenNoResource()
    {
        var brush = Assert.IsType<SolidColorBrush>(PaletteTokens.SurfacePanelBrush);
        Assert.Equal(Color.Parse("#1A2540"), brush.Color);
    }

    [Fact]
    public void TextPrimaryBrushOrFallback_FallsBackToNavyPrimary_WhenNoResource()
    {
        var brush = Assert.IsType<SolidColorBrush>(PaletteTokens.TextPrimaryBrushOrFallback);
        Assert.Equal(Color.Parse("#E3E4F4"), brush.Color);
    }

    [Fact]
    public void CreateSuccessStatusFallbackBrush_UsesSuccessGreen()
    {
        var brush = Assert.IsType<SolidColorBrush>(PaletteTokens.CreateSuccessStatusFallbackBrush());
        Assert.Equal(Color.Parse("#28A745"), brush.Color);
    }

    [Fact]
    public void GetBrush_UsesFallback_WhenResourceMissing()
    {
        var fallback = new SolidColorBrush(Color.Parse("#28A745"));
        var brush = PaletteTokens.GetBrush("Missing.StatusBrush", fallback);
        Assert.Same(fallback, brush);
    }

    [Fact]
    public void GetColor_UsesFallback_WhenResourceMissing()
    {
        var fallback = Color.Parse("#243352");
        Assert.Equal(fallback, PaletteTokens.GetColor("Missing.SurfaceColor", fallback));
    }
}
