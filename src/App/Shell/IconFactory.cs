using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lucide.Avalonia;
using Zaide.UI.DesignSystem;

namespace Zaide.App.Shell;

public static class IconFactory
{
    /// <summary>
    /// Creates an icon with a live theme brush binding on <see cref="LucideIcon.Foreground"/>.
    /// </summary>
    public static Control Create(string resourceKey, string foregroundBrushKey, double size = 16)
    {
        var icon = Create(resourceKey, foreground: null, size);
        ThemeBinding.SetBrush(icon, LucideIcon.ForegroundProperty, foregroundBrushKey);
        return icon;
    }

    public static Control Create(string resourceKey, IBrush? foreground, double size = 16)
    {
        var kind = IconLucideMap.Resolve(resourceKey);
        var strokeWidth = Math.Clamp(size / 8.0, 1.25, 2.0);

        var icon = new LucideIcon
        {
            Size = size,
            StrokeWidth = strokeWidth,
            Foreground = foreground,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // Lucide resolves path geometry when Kind is set; defer until attach so
        // headless/unit tests can construct views without a render platform.
        icon.AttachedToVisualTree += (_, _) =>
        {
            if (icon.Kind is null)
                icon.Kind = kind;
        };

        return icon;
    }

    public static void SetForeground(Control icon, IBrush? foreground)
    {
        if (icon is LucideIcon lucideIcon)
            lucideIcon.Foreground = foreground;
    }
}
