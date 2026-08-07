using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

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

    /// <summary>
    /// Binds <paramref name="property"/> on <paramref name="target"/> to a theme
    /// brush and re-applies it when <see cref="Application.ActualThemeVariant"/>
    /// changes. Interaction state styles in <see cref="Controls.ControlThemeCatalog"/>
    /// use <see cref="DynamicResourceExtension"/> directly; this helper covers
    /// imperative code-built surfaces.
    /// </summary>
    public static void SetBrush(AvaloniaObject target, AvaloniaProperty property, string resourceKey)
    {
        if (target is Control control)
        {
            void Apply() => target.SetValue(property, GetBrush(resourceKey));
            Apply();
            SubscribeVariantChanged(control, Apply);
            return;
        }

        target.SetValue(property, GetBrush(resourceKey));
    }

    /// <summary>
    /// Binds a color-valued property to a theme resource and re-applies on variant change.
    /// </summary>
    public static void SetColor(AvaloniaObject target, AvaloniaProperty property, string resourceKey)
    {
        if (target is Control control)
        {
            void Apply() => target.SetValue(property, GetColor(resourceKey));
            Apply();
            SubscribeVariantChanged(control, Apply);
            return;
        }

        target.SetValue(property, GetColor(resourceKey));
    }

    /// <summary>
    /// Invokes <paramref name="onVariantChanged"/> when the application theme
    /// variant changes. Unsubscribes when <paramref name="host"/> detaches from
    /// the visual tree. Use for imperative brush assignments that depend on
    /// runtime state (selection, active/inactive) where a single resource key
    /// cannot be bound statically.
    /// </summary>
    public static void SubscribeVariantChanged(Control host, Action onVariantChanged)
    {
        var app = ResolveApplication();
        EventHandler? handler = null;
        handler = (_, _) => onVariantChanged();
        app.ActualThemeVariantChanged += handler;

        void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            app.ActualThemeVariantChanged -= handler;
            host.DetachedFromVisualTree -= OnDetached;
        }

        host.DetachedFromVisualTree += OnDetached;
    }

    public static IBrush GetBrush(string resourceKey)
    {
        var app = ResolveApplication();

        foreach (var variant in GetVariantResolutionOrder(app))
        {
            if (app.TryGetResource(resourceKey, variant, out var value) &&
                value is IBrush resolvedBrush)
            {
                return resolvedBrush;
            }
        }

        throw new InvalidOperationException($"Resource '{resourceKey}' not found or is not a brush.");
    }

    public static Color GetColor(string resourceKey)
    {
        var app = ResolveApplication();

        foreach (var variant in GetVariantResolutionOrder(app))
        {
            if (!app.TryGetResource(resourceKey, variant, out var value))
                continue;

            return value switch
            {
                Color color => color,
                ISolidColorBrush brush => brush.Color,
                _ => throw new InvalidOperationException($"Resource '{resourceKey}' is not a color."),
            };
        }

        throw new InvalidOperationException($"Resource '{resourceKey}' not found.");
    }

    internal static DynamicResourceExtension DynamicResource(string resourceKey) =>
        new() { ResourceKey = resourceKey };

    private static IEnumerable<ThemeVariant> GetVariantResolutionOrder(Application app)
    {
        var primary = GetThemeVariant(app);
        yield return primary;
        if (primary != ThemeVariant.Light)
            yield return ThemeVariant.Light;
        if (primary != ThemeVariant.Default)
            yield return ThemeVariant.Default;
    }

    private static ThemeVariant GetThemeVariant(Application app)
    {
        if (!app.CheckAccess())
            return ThemeVariant.Light;

        var actual = app.ActualThemeVariant;
        if (actual != ThemeVariant.Default)
            return actual;

        var requested = app.RequestedThemeVariant;
        return requested != ThemeVariant.Default ? requested : ThemeVariant.Light;
    }

    private static Application ResolveApplication() =>
        TestApplication
        ?? Application.Current as Application
        ?? throw new InvalidOperationException("Application is not initialized.");
}
