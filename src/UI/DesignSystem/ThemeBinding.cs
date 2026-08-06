using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace Zaide.UI.DesignSystem;

/// <summary>
/// Resolves theme-aware brushes and colors via the active theme variant so they
/// update when <see cref="Application.ActualThemeVariant"/> changes.
/// </summary>
internal static class ThemeBinding
{
    /// <summary>
    /// Test-only application instance when <see cref="Application.Current"/> is unset.
    /// </summary>
    internal static Application? TestApplication { get; set; }

    /// <summary>
    /// The active theme variant, resolved from the current application. Falls
    /// back to <see cref="ThemeVariant.Light"/> off the UI thread.
    /// </summary>
    internal static ThemeVariant CurrentVariant
    {
        get
        {
            var app = ResolveApplication();
            return GetThemeVariant(app);
        }
    }

    public static IBrush GetBrush(string resourceKey)
    {
        var app = ResolveApplication();

        if (app.TryGetResource(resourceKey, GetThemeVariant(app), out var value) &&
            value is IBrush resolvedBrush)
        {
            return resolvedBrush;
        }

        throw new InvalidOperationException($"Resource '{resourceKey}' not found or is not a brush.");
    }

    public static Color GetColor(string resourceKey)
    {
        var app = ResolveApplication();

        if (!app.TryGetResource(resourceKey, GetThemeVariant(app), out var value))
        {
            throw new InvalidOperationException($"Resource '{resourceKey}' not found.");
        }

        return value switch
        {
            Color color => color,
            ISolidColorBrush brush => brush.Color,
            _ => throw new InvalidOperationException($"Resource '{resourceKey}' is not a color."),
        };
    }

    private static ThemeVariant GetThemeVariant(Application app)
    {
        if (!app.CheckAccess())
            return ThemeVariant.Light;

        return app.ActualThemeVariant;
    }

    private static Application ResolveApplication() =>
        Application.Current as Application
        ?? TestApplication
        ?? throw new InvalidOperationException("Application is not initialized.");
}
