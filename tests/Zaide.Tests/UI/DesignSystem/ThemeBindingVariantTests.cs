using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Xunit;
using Zaide.Tests.Infrastructure;
using Zaide.UI.DesignSystem;

namespace Zaide.Tests.UI.DesignSystem;

[Collection("AvaloniaUiInitialization")]
public class ThemeBindingVariantTests
{
    [Fact]
    public void GetColor_ReturnsDifferentPrimaryAccent_WhenVariantFlips()
    {
        var app = CreateAppOnCurrentThread();
        var previousVariant = app.RequestedThemeVariant;

        try
        {
            app.RequestedThemeVariant = ThemeVariant.Light;
            Assert.Equal(ThemeVariant.Light, ThemeBinding.CurrentVariant);
            var lightAccent = ThemeBinding.GetColor("PrimaryAccentBrushColor");

            app.RequestedThemeVariant = ThemeVariant.Dark;
            Assert.Equal(ThemeVariant.Dark, ThemeBinding.CurrentVariant);
            var darkAccent = ThemeBinding.GetColor("PrimaryAccentBrushColor");

            Assert.NotEqual(lightAccent, darkAccent);
        }
        finally
        {
            app.RequestedThemeVariant = previousVariant;
            ThemeBinding.TestApplication = ReactiveUiTestBootstrap.EnsureApplication();
        }
    }

    [Fact]
    public void GetBrush_ReturnsDifferentTextPrimary_WhenVariantFlips()
    {
        var app = CreateAppOnCurrentThread();
        var previousVariant = app.RequestedThemeVariant;

        try
        {
            app.RequestedThemeVariant = ThemeVariant.Light;
            var lightBrush = ThemeBinding.GetBrush("TextPrimaryBrush");

            app.RequestedThemeVariant = ThemeVariant.Dark;
            var darkBrush = ThemeBinding.GetBrush("TextPrimaryBrush");

            Assert.IsAssignableFrom<ISolidColorBrush>(lightBrush);
            Assert.IsAssignableFrom<ISolidColorBrush>(darkBrush);
            Assert.NotEqual(
                ((ISolidColorBrush)lightBrush).Color,
                ((ISolidColorBrush)darkBrush).Color);
        }
        finally
        {
            app.RequestedThemeVariant = previousVariant;
            ThemeBinding.TestApplication = ReactiveUiTestBootstrap.EnsureApplication();
        }
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
