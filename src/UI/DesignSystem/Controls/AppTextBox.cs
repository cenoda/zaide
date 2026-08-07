using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;

namespace Zaide.UI.DesignSystem;

/// <summary>
/// Token-backed <see cref="TextBox"/> factories for commit inputs, chat, search,
/// and command palette fields.
/// </summary>
internal static class AppTextBox
{
    private const string BorderFocusBrushKey = "BorderFocusBrush";
    private const string SurfaceRaised1BrushKey = "SurfaceRaised1Brush";
    private const string BorderSubtleBrushKey = "BorderSubtleBrush";

    private static readonly ControlTheme InputTheme = CreateInputTheme();

    /// <summary>Multiline-capable panel input (commit message, chat).</summary>
    internal static TextBox Input(
        string? placeholder = null,
        bool acceptsReturn = false,
        double minHeight = 32,
        double? maxHeight = null)
    {
        var textBox = CreateBase(placeholder);
        textBox.AcceptsReturn = acceptsReturn;
        textBox.TextWrapping = acceptsReturn ? TextWrapping.Wrap : TextWrapping.NoWrap;
        textBox.MinHeight = minHeight;
        if (maxHeight is not null)
            textBox.MaxHeight = maxHeight.Value;
        textBox.Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingMd, LayoutTokens.SpacingSm);
        return textBox;
    }

    /// <summary>Full-width search field (command palette).</summary>
    internal static TextBox Search(string placeholder = "Search...")
    {
        var textBox = CreateBase(placeholder);
        textBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        textBox.FontSize = TypographyTokens.FontSizeMd + 2;
        return textBox;
    }

    /// <summary>Compact inline search/replace field (editor search bar).</summary>
    internal static TextBox Inline(string placeholder, double width = 200)
    {
        var textBox = CreateBase(placeholder);
        textBox.Width = width;
        return textBox;
    }

    private static TextBox CreateBase(string? placeholder)
    {
        var textBox = new TextBox
        {
            PlaceholderText = placeholder ?? string.Empty,
            BorderThickness = LayoutTokens.NoneThickness,
            FontSize = TypographyTokens.FontSizeSm + 1,
            Theme = InputTheme,
        };
        ThemeBinding.SetBrush(textBox, TextBox.BackgroundProperty, SurfaceRaised1BrushKey);
        ThemeBinding.SetBrush(textBox, TextBox.ForegroundProperty, "TextPrimaryBrush");
        ThemeBinding.SetBrush(textBox, TextBox.BorderBrushProperty, BorderSubtleBrushKey);
        return textBox;
    }

    private static ControlTheme CreateInputTheme()
    {
        var theme = new ControlTheme(typeof(TextBox))
        {
            Setters =
            {
                new Setter(TextBox.BorderThicknessProperty, LayoutTokens.NoneThickness),
            },
        };

        theme.Children.Add(CreateFocusRingStateStyle(s => s.Nesting().Class(":focus")));
        theme.Children.Add(CreateDisabledStateStyle(s => s.Nesting().Class(":disabled")));
        return theme;
    }

    private static Style CreateFocusRingStateStyle(Func<Selector?, Selector> selector)
    {
        var style = new Style(selector);
        style.Setters.Add(new Setter(TextBox.BorderBrushProperty, DynamicBrush(BorderFocusBrushKey)));
        style.Setters.Add(new Setter(TextBox.BorderThicknessProperty, new Thickness(1)));
        return style;
    }

    private static Style CreateDisabledStateStyle(Func<Selector?, Selector> selector)
    {
        var style = new Style(selector);
        style.Setters.Add(new Setter(Visual.OpacityProperty, 0.55));
        return style;
    }

    private static DynamicResourceExtension DynamicBrush(string resourceKey) =>
        ThemeBinding.DynamicResource(resourceKey);
}
