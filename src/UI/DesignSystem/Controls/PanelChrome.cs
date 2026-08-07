using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Zaide.UI.DesignSystem;

/// <summary>
/// Presentation-only panel chrome helpers (headers, dividers, empty states).
/// </summary>
internal static class PanelChrome
{
    /// <summary>1px horizontal separator using <c>SeparatorBrush</c>.</summary>
    internal static Border Divider()
    {
        var divider = new Border
        {
            Height = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        ThemeBinding.SetBrush(divider, Border.BackgroundProperty, "SeparatorBrush");
        return divider;
    }

    /// <summary>Caption-style empty-state message.</summary>
    internal static TextBlock EmptyState(string message)
    {
        var text = TextStyles.Caption(message);
        text.Margin = LayoutTokens.Symmetric(LayoutTokens.SpacingMd, LayoutTokens.SpacingSm);
        text.TextWrapping = TextWrapping.Wrap;
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.TextAlignment = TextAlignment.Center;
        return text;
    }

    /// <summary>Section title using header typography.</summary>
    internal static TextBlock SectionTitle(string title)
    {
        var text = TextStyles.Header(title);
        text.VerticalAlignment = VerticalAlignment.Center;
        return text;
    }

    /// <summary>Section header row with optional trailing action control.</summary>
    internal static Grid SectionHeader(
        Control primary,
        Control? trailing = null,
        Thickness? margin = null)
    {
        var grid = new Grid
        {
            Margin = margin ?? LayoutTokens.Inset(
                LayoutTokens.SpacingMd,
                LayoutTokens.SpacingLg,
                LayoutTokens.SpacingMd,
                LayoutTokens.SpacingSm),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
            Children = { primary },
        };
        Grid.SetColumn(primary, 0);

        if (trailing is not null)
        {
            grid.Children.Add(trailing);
            Grid.SetColumn(trailing, 2);
        }

        return grid;
    }

    /// <summary>Section header row with a title string and optional trailing control.</summary>
    internal static Grid SectionHeader(
        string title,
        Control? trailing = null,
        Thickness? margin = null) =>
        SectionHeader(SectionTitle(title), trailing, margin);

    /// <summary>Dock-top header bar with bottom separator border.</summary>
    internal static Border HeaderBar(Control child, Thickness? padding = null)
    {
        var bar = new Border
        {
            Child = child,
            Padding = padding ?? LayoutTokens.Inset(0, 0, 0, LayoutTokens.SpacingXs),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        ThemeBinding.SetBrush(bar, Border.BorderBrushProperty, "SeparatorBrush");
        return bar;
    }
}
