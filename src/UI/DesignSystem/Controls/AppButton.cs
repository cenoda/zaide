using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;

namespace Zaide.UI.DesignSystem;

/// <summary>
/// Token-backed <see cref="Button"/> and icon-surface factories for shell and panel
/// toolbars. Interaction themes mirror <see cref="ControlThemeCatalog"/> tokens.
/// </summary>
internal static class AppButton
{
    private const string OverlayHoverBrushKey = "OverlayHoverBrush";
    private const string OverlayPressedBrushKey = "OverlayPressedBrush";
    private const string BorderFocusBrushKey = "BorderFocusBrush";
    private const string AccentBrushKey = "AccentBrush";
    private const string AccentHoverBrushKey = "AccentHoverBrush";
    private const string AccentPressedBrushKey = "AccentPressedBrush";

    private static readonly ControlTheme GhostButtonTheme = CreateGhostButtonTheme();
    private static readonly ControlTheme PrimaryButtonTheme = CreatePrimaryButtonTheme();

    /// <summary>Accent-filled primary action (commit, push).</summary>
    internal static Button Primary(object content, double height = 30)
    {
        var button = new Button
        {
            Content = content,
            Height = height,
            Background = ThemeBinding.GetBrush(AccentBrushKey),
            Foreground = ThemeBinding.GetBrush("TextOnAccentBrush"),
            BorderThickness = LayoutTokens.NoneThickness,
            FontSize = TypographyTokens.FontSizeSm + 1,
            Cursor = TryCreateHandCursor(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Theme = PrimaryButtonTheme,
        };
        return button;
    }

    /// <summary>Low-emphasis text action (stage all, unstage all).</summary>
    internal static Button Secondary(object content, double fontSize = 11)
    {
        var button = Ghost(content, LayoutTokens.Inset(
            LayoutTokens.SpacingSm,
            LayoutTokens.SpacingXxs,
            LayoutTokens.SpacingSm,
            LayoutTokens.SpacingXxs));
        button.FontSize = fontSize;
        button.Foreground = ThemeBinding.GetBrush("TextSecondaryBrush");
        button.HorizontalAlignment = HorizontalAlignment.Right;
        button.VerticalAlignment = VerticalAlignment.Center;
        return button;
    }

    /// <summary>Transparent toolbar/settings control with overlay hover.</summary>
    internal static Button Ghost(object content, Thickness? padding = null)
    {
        var button = new Button
        {
            Content = content,
            Background = Brushes.Transparent,
            BorderThickness = LayoutTokens.NoneThickness,
            Foreground = ThemeBinding.GetBrush("TextSecondaryBrush"),
            Padding = padding ?? LayoutTokens.Symmetric(LayoutTokens.SpacingXs, LayoutTokens.SpacingXxs),
            CornerRadius = LayoutTokens.RadiusSm,
            Cursor = TryCreateHandCursor(),
            VerticalAlignment = VerticalAlignment.Center,
            Theme = GhostButtonTheme,
        };
        return button;
    }

    /// <summary>Square icon button (refresh, search chevrons).</summary>
    internal static Button Icon(
        object content,
        double size = 24,
        Thickness? padding = null)
    {
        var button = Ghost(content, padding ?? LayoutTokens.NoneThickness);
        button.Width = size;
        button.Height = size;
        button.CornerRadius = LayoutTokens.RadiusSm;
        button.HorizontalAlignment = HorizontalAlignment.Center;
        button.VerticalAlignment = VerticalAlignment.Center;
        return button;
    }

    /// <summary>
    /// Compact mode label button (bottom panel host terminal/problems tabs).
    /// </summary>
    internal static Button ToolbarLabel(
        string label,
        Thickness margin,
        bool smallFont = false)
    {
        var button = Ghost(
            label,
            LayoutTokens.Symmetric(LayoutTokens.SpacingSm, LayoutTokens.SpacingXxs));
        button.Margin = margin;
        if (smallFont)
            button.FontSize = TypographyTokens.FontSizeSm;
        return button;
    }

    /// <summary>
    /// Icon-only interactive surface (NavBar) using
    /// <see cref="ControlThemeCatalog.InteractiveSurfaceThemeKey"/>.
    /// </summary>
    internal static Border IconSurface(
        object content,
        double size = 32,
        string? tooltip = null)
    {
        var surface = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = LayoutTokens.RadiusMd,
            Cursor = TryCreateHandCursor(),
            Child = content as Control ?? new ContentControl { Content = content },
        };

        if (tooltip is not null)
            ToolTip.SetTip(surface, tooltip);

        ControlThemeCatalog.ApplyInteractiveSurface(surface);
        return surface;
    }

    private static ControlTheme CreateGhostButtonTheme()
    {
        var theme = new ControlTheme(typeof(Button))
        {
            Setters =
            {
                new Setter(Button.BackgroundProperty, Brushes.Transparent),
                new Setter(Button.BorderThicknessProperty, LayoutTokens.NoneThickness),
            },
        };

        theme.Children.Add(CreateStateStyle(
            s => s.Nesting().Class(":pointerover"),
            Button.BackgroundProperty,
            OverlayHoverBrushKey));
        theme.Children.Add(CreateStateStyle(
            s => s.Nesting().Class(":pressed"),
            Button.BackgroundProperty,
            OverlayPressedBrushKey));
        theme.Children.Add(CreateFocusRingStateStyle(s => s.Nesting().Class(":focus")));
        theme.Children.Add(CreateDisabledStateStyle(s => s.Nesting().Class(":disabled")));
        return theme;
    }

    private static ControlTheme CreatePrimaryButtonTheme()
    {
        var theme = new ControlTheme(typeof(Button))
        {
            Setters =
            {
                new Setter(Button.BorderThicknessProperty, LayoutTokens.NoneThickness),
            },
        };

        theme.Children.Add(CreateStateStyle(
            s => s.Nesting().Class(":pointerover"),
            Button.BackgroundProperty,
            AccentHoverBrushKey));
        theme.Children.Add(CreateStateStyle(
            s => s.Nesting().Class(":pressed"),
            Button.BackgroundProperty,
            AccentPressedBrushKey));
        theme.Children.Add(CreateFocusRingStateStyle(s => s.Nesting().Class(":focus")));
        theme.Children.Add(CreateDisabledStateStyle(s => s.Nesting().Class(":disabled")));
        return theme;
    }

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
        style.Setters.Add(new Setter(Button.BorderBrushProperty, DynamicBrush(BorderFocusBrushKey)));
        style.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(1)));
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

    private static Cursor? TryCreateHandCursor()
    {
        try
        {
            return new Cursor(StandardCursorType.Hand);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
