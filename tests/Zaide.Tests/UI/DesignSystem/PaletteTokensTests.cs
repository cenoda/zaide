using System;
using Avalonia.Media;
using Avalonia.Styling;
using Xunit;
using Zaide.Tests.Infrastructure;
using Zaide.UI.DesignSystem;

namespace Zaide.Tests.UI.DesignSystem;

[Collection("AvaloniaUiInitialization")]
public class PaletteTokensTests
{
    [Fact]
    public void TextPrimaryBrush_ResolvesFromLightTheme()
    {
        ReactiveUiTestBootstrap.EnsureApplication();
        Assert.IsAssignableFrom<IBrush>(PaletteTokens.TextPrimaryBrush);
        Assert.Equal(Color.Parse("#15171E"), ThemeBinding.GetColor("TextPrimaryBrushColor"));
    }

    [Fact]
    public void PrimaryAccentBrush_ResolvesFromLightTheme()
    {
        ReactiveUiTestBootstrap.EnsureApplication();
        Assert.IsAssignableFrom<IBrush>(PaletteTokens.PrimaryAccentBrush);
        Assert.Equal(Color.Parse("#1B6FD4"), ThemeBinding.GetColor("PrimaryAccentBrushColor"));
    }

    [Fact]
    public void TextSecondaryBrush_ResolvesFromLightTheme()
    {
        ReactiveUiTestBootstrap.EnsureApplication();
        Assert.IsAssignableFrom<IBrush>(PaletteTokens.TextSecondaryBrush);
        Assert.Equal(Color.Parse("#525A68"), ThemeBinding.GetColor("TextSecondaryBrushColor"));
    }

    [Fact]
    public void SurfaceRaisedColor_ResolvesFromLightTheme()
    {
        ReactiveUiTestBootstrap.EnsureApplication();
        Assert.Equal(Color.Parse("#D5DAE3"), PaletteTokens.SurfaceRaisedColor);
    }

    [Fact]
    public void PrimaryAccentColor_ResolvesFromLightTheme()
    {
        ReactiveUiTestBootstrap.EnsureApplication();
        Assert.Equal(Color.Parse("#1B6FD4"), PaletteTokens.PrimaryAccentColor);
    }

    [Fact]
    public void SuccessBrush_ResolvesFromLightTheme()
    {
        ReactiveUiTestBootstrap.EnsureApplication();
        Assert.IsAssignableFrom<IBrush>(PaletteTokens.SuccessBrush);
        Assert.Equal(Color.Parse("#1D8348"), ThemeBinding.GetColor("SuccessBrushColor"));
    }

    [Fact]
    public void SurfacePanelBrush_ResolvesFromLightTheme()
    {
        ReactiveUiTestBootstrap.EnsureApplication();
        Assert.IsAssignableFrom<IBrush>(PaletteTokens.SurfacePanelBrush);
        Assert.Equal(Color.Parse("#EBEDF2"), ThemeBinding.GetColor("SurfacePanelBrushColor"));
    }

    [Fact]
    public void GetBrush_ThrowsWhenResourceMissing()
    {
        ReactiveUiTestBootstrap.EnsureApplication();
        Assert.Throws<InvalidOperationException>(() => PaletteTokens.GetBrush("Missing.StatusBrush"));
    }

    [Fact]
    public void GetColor_ThrowsWhenResourceMissing()
    {
        ReactiveUiTestBootstrap.EnsureApplication();
        Assert.Throws<InvalidOperationException>(() => PaletteTokens.GetColor("Missing.SurfaceColor"));
    }
}
