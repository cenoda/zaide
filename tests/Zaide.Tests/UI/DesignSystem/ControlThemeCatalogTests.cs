using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using Xunit;
using Zaide.Tests.Infrastructure;
using Zaide.UI.DesignSystem;

namespace Zaide.Tests.UI.DesignSystem;

[Collection("AvaloniaUiInitialization")]
public class ControlThemeCatalogTests
{
    [Fact]
    public void Register_ExposesInteractiveSurfaceThemeInResources()
    {
        var app = ReactiveUiTestBootstrap.EnsureApplication();

        Assert.True(app.Resources.ContainsKey(ControlThemeCatalog.InteractiveSurfaceThemeKey));
        Assert.IsType<ControlTheme>(app.Resources[ControlThemeCatalog.InteractiveSurfaceThemeKey]);
    }

    [Fact]
    public void Register_AddsInteractiveSurfaceStyle()
    {
        var app = ReactiveUiTestBootstrap.EnsureApplication();

        Assert.Contains(
            app.Styles,
            style => StyleTargetsInteractiveBorder(style));
    }

    [Fact]
    public void FocusRingStyle_UsesBorderFocusToken()
    {
        var style = ControlThemeCatalog.CreateFocusRingStyle();
        var setter = style.Setters
            .OfType<Setter>()
            .First(s => s.Property == Border.BorderBrushProperty);

        var dynamicResource = Assert.IsType<DynamicResourceExtension>(setter.Value);
        Assert.Equal("BorderFocusBrush", dynamicResource.ResourceKey);
    }

    [Fact]
    public void InteractiveSurfaceTheme_UsesBorderFocusTokenForFocusState()
    {
        var app = ReactiveUiTestBootstrap.EnsureApplication();
        var theme = Assert.IsType<ControlTheme>(
            app.Resources[ControlThemeCatalog.InteractiveSurfaceThemeKey]);

        var focusStyle = theme.Children
            .OfType<Style>()
            .First(style => StyleHasFocusBorderBrushSetter(style));

        var setter = focusStyle.Setters
            .OfType<Setter>()
            .First(s => s.Property == Border.BorderBrushProperty);
        var dynamicResource = Assert.IsType<DynamicResourceExtension>(setter.Value);
        Assert.Equal("BorderFocusBrush", dynamicResource.ResourceKey);
    }

    private static bool StyleTargetsInteractiveBorder(IStyle style) =>
        style is Style concrete &&
        concrete.Selector?.ToString()?.Contains(ControlThemeCatalog.InteractiveClass) == true &&
        concrete.Selector?.ToString()?.Contains("Border") == true;

    private static bool StyleHasFocusBorderBrushSetter(Style style) =>
        style.Setters
            .OfType<Setter>()
            .Any(s =>
                s.Property == Border.BorderBrushProperty &&
                s.Value is DynamicResourceExtension { ResourceKey: "BorderFocusBrush" });
}
