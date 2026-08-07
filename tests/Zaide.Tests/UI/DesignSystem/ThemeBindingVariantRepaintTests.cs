using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Xunit;
using Zaide.Tests.Infrastructure;
using Zaide.UI.DesignSystem;

namespace Zaide.Tests.UI.DesignSystem;

[Collection("AvaloniaUiInitialization")]
public class ThemeBindingVariantRepaintTests
{
    [Fact]
    public void SetBrush_UpdatesBorderBackground_WhenVariantFlips()
    {
        var app = CreateAppOnCurrentThread();
        var previousVariant = app.RequestedThemeVariant;

        try
        {
            var border = new Border { Width = 10, Height = 10 };
            ThemeBinding.SetBrush(border, Border.BackgroundProperty, "SurfaceBaseBrush");

            app.RequestedThemeVariant = ThemeVariant.Light;
            var lightColor = GetSolidColor(border.Background);

            app.RequestedThemeVariant = ThemeVariant.Dark;
            var darkColor = GetSolidColor(border.Background);

            Assert.NotEqual(lightColor, darkColor);
        }
        finally
        {
            app.RequestedThemeVariant = previousVariant;
            ThemeBinding.TestApplication = ReactiveUiTestBootstrap.EnsureApplication();
        }
    }

    [Fact]
    public void SetBrush_UpdatesTextForeground_WhenVariantFlips()
    {
        var app = CreateAppOnCurrentThread();
        var previousVariant = app.RequestedThemeVariant;

        try
        {
            var text = new TextBlock { Text = "label" };
            ThemeBinding.SetBrush(text, TextBlock.ForegroundProperty, "TextPrimaryBrush");

            app.RequestedThemeVariant = ThemeVariant.Light;
            var lightColor = GetSolidColor(text.Foreground);

            app.RequestedThemeVariant = ThemeVariant.Dark;
            var darkColor = GetSolidColor(text.Foreground);

            Assert.NotEqual(lightColor, darkColor);
        }
        finally
        {
            app.RequestedThemeVariant = previousVariant;
            ThemeBinding.TestApplication = ReactiveUiTestBootstrap.EnsureApplication();
        }
    }

    [Fact]
    public void AppButton_Primary_RepaintsSurface_WhenVariantFlips()
    {
        var app = CreateAppOnCurrentThread();
        var previousVariant = app.RequestedThemeVariant;

        try
        {
            var button = AppButton.Primary("Commit");

            app.RequestedThemeVariant = ThemeVariant.Light;
            var lightColor = GetSolidColor(button.Background);

            app.RequestedThemeVariant = ThemeVariant.Dark;
            var darkColor = GetSolidColor(button.Background);

            Assert.NotEqual(lightColor, darkColor);
        }
        finally
        {
            app.RequestedThemeVariant = previousVariant;
            ThemeBinding.TestApplication = ReactiveUiTestBootstrap.EnsureApplication();
        }
    }

    [Fact]
    public void PanelChrome_Divider_RepaintsSeparator_WhenVariantFlips()
    {
        var app = CreateAppOnCurrentThread();
        var previousVariant = app.RequestedThemeVariant;

        try
        {
            var divider = PanelChrome.Divider();

            app.RequestedThemeVariant = ThemeVariant.Light;
            var lightColor = GetSolidColor(divider.Background);

            app.RequestedThemeVariant = ThemeVariant.Dark;
            var darkColor = GetSolidColor(divider.Background);

            Assert.NotEqual(lightColor, darkColor);
        }
        finally
        {
            app.RequestedThemeVariant = previousVariant;
            ThemeBinding.TestApplication = ReactiveUiTestBootstrap.EnsureApplication();
        }
    }

    private static Color GetSolidColor(IBrush? brush)
    {
        var solid = Assert.IsAssignableFrom<ISolidColorBrush>(brush);
        return solid.Color;
    }

    private static Zaide.App.Composition.App CreateAppOnCurrentThread()
    {
        ReactiveUiTestBootstrap.EnsureInitialized();
        var app = new Zaide.App.Composition.App();
        app.Initialize();
        ThemeBinding.TestApplication = app;
        return app;
    }
}
