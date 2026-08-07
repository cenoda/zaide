using System;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;

namespace Zaide.UI.DesignSystem;

/// <summary>
/// Builds and registers shared <see cref="ControlTheme"/> and <see cref="Style"/>
/// objects for interaction states (hover, pressed, selected, focus, disabled).
/// Themes reference semantic tokens via <see cref="DynamicResourceExtension"/> so
/// they follow <see cref="Application.ActualThemeVariant"/>.
/// </summary>
internal static class ControlThemeCatalog
{
    /// <summary>Named theme for interactive <see cref="Border"/> surfaces (rows, chrome).</summary>
    internal const string InteractiveSurfaceThemeKey = "Zaide.ControlTheme.InteractiveSurface";

    /// <summary>Style class applied to controls that use the shared interaction layer.</summary>
    internal const string InteractiveClass = "zaide-interactive";

    /// <summary>Style class for selected list/tree rows.</summary>
    internal const string SelectedClass = "zaide-selected";

    private const string OverlayHoverBrushKey = "OverlayHoverBrush";
    private const string OverlayPressedBrushKey = "OverlayPressedBrush";
    private const string OverlaySelectedBrushKey = "OverlaySelectedBrush";
    private const string BorderFocusBrushKey = "BorderFocusBrush";

    private static readonly ConditionalWeakTable<Application, object> Registered = new();

    /// <summary>
    /// Registers catalog themes into <paramref name="app"/> resources and styles.
    /// Idempotent per <see cref="Application"/> instance.
    /// </summary>
    internal static void Register(Application app)
    {
        if (Registered.TryGetValue(app, out _))
            return;

        RegisterResources(app);
        RegisterStyles(app);
        Registered.Add(app, null!);
    }

    /// <summary>Resets registration tracking for test isolation.</summary>
    internal static void ResetRegistrationForTests() =>
        Registered.Clear();

    /// <summary>
    /// Applies the shared interactive surface theme and style class to a
    /// <see cref="Border"/> host (list rows, icon surfaces).
    /// </summary>
    internal static void ApplyInteractiveSurface(Border border)
    {
        border.Classes.Add(InteractiveClass);

        var app = ResolveApplication();
        if (app?.Resources.TryGetValue(InteractiveSurfaceThemeKey, out var theme) == true
            && theme is ControlTheme controlTheme)
        {
            border.Theme = controlTheme;
        }
    }

    private static Application? ResolveApplication() =>
        Application.Current as Application ?? ThemeBinding.TestApplication;

    private static void RegisterResources(Application app)
    {
        app.Resources[InteractiveSurfaceThemeKey] = CreateInteractiveSurfaceTheme();
    }

    private static void RegisterStyles(Application app)
    {
        app.Styles.Add(CreateInteractiveSurfaceStyle());
    }

    private static ControlTheme CreateInteractiveSurfaceTheme()
    {
        var theme = new ControlTheme(typeof(Border))
        {
            Setters =
            {
                new Setter(Border.BackgroundProperty, Brushes.Transparent),
                new Setter(Border.BorderThicknessProperty, LayoutTokens.NoneThickness),
            },
        };

        theme.Children.Add(CreateStateStyle(s => s.Nesting().Class(":pointerover"),
            Border.BackgroundProperty, OverlayHoverBrushKey));
        theme.Children.Add(CreateStateStyle(s => s.Nesting().Class(":pressed"),
            Border.BackgroundProperty, OverlayPressedBrushKey));
        theme.Children.Add(CreateStateStyle(s => s.Nesting().Class(SelectedClass),
            Border.BackgroundProperty, OverlaySelectedBrushKey));
        theme.Children.Add(CreateFocusRingStateStyle(s => s.Nesting().Class(":focus")));
        theme.Children.Add(CreateDisabledStateStyle(s => s.Nesting().Class(":disabled")));

        return theme;
    }

    private static Style CreateInteractiveSurfaceStyle()
    {
        var style = new Style(s => s.OfType<Border>().Class(InteractiveClass));
        style.Setters.Add(new Setter(Border.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Border.BorderThicknessProperty, LayoutTokens.NoneThickness));

        style.Children.Add(CreateStateStyle(s => s.Nesting().Class(":pointerover"),
            Border.BackgroundProperty, OverlayHoverBrushKey));
        style.Children.Add(CreateStateStyle(s => s.Nesting().Class(":pressed"),
            Border.BackgroundProperty, OverlayPressedBrushKey));
        style.Children.Add(CreateStateStyle(s => s.Nesting().Class(SelectedClass),
            Border.BackgroundProperty, OverlaySelectedBrushKey));
        style.Children.Add(CreateFocusRingStateStyle(s => s.Nesting().Class(":focus")));
        style.Children.Add(CreateDisabledStateStyle(s => s.Nesting().Class(":disabled")));

        return style;
    }

    /// <summary>
    /// Shared <see cref="BorderFocusBrush"/> focus ring for interactive borders.
    /// </summary>
    internal static Style CreateFocusRingStyle() =>
        CreateFocusRingStateStyle(s => s.OfType<Border>().Class(InteractiveClass).Class(":focus"));

    private static Style CreateStateStyle(
        Func<Selector?, Selector> selector,
        AvaloniaProperty property,
        string brushKey)
    {
        var style = new Style(selector);
        style.Setters.Add(new Setter(property, DynamicBrush(brushKey)));
        return style;
    }

    private static Style CreateFocusRingStateStyle(Func<Selector?, Selector> selector)
    {
        var style = new Style(selector);
        style.Setters.Add(new Setter(Border.BorderBrushProperty, DynamicBrush(BorderFocusBrushKey)));
        style.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1)));
        return style;
    }

    private static Style CreateDisabledStateStyle(Func<Selector?, Selector> selector)
    {
        var style = new Style(selector);
        style.Setters.Add(new Setter(Visual.OpacityProperty, 0.55));
        return style;
    }

    private static DynamicResourceExtension DynamicBrush(string resourceKey) =>
        new() { ResourceKey = resourceKey };
}
