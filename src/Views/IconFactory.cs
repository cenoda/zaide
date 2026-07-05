using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace Zaide.Views;

public static class IconFactory
{
    public static Viewbox Create(string resourceKey, IBrush? foreground, double size = 16)
    {
        var geometry = ResolveIconGeometry(resourceKey);
        var path = new Path
        {
            Data = geometry,
            Width = 256,
            Height = 256,
            Stroke = foreground,
            StrokeThickness = 16,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            Stretch = Stretch.Uniform,
            IsHitTestVisible = false
        };

        return new Viewbox
        {
            Width = size,
            Height = size,
            Child = path,
            Stretch = Stretch.Uniform,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static Geometry ResolveIconGeometry(string resourceKey)
    {
        var app = Application.Current
            ?? throw new InvalidOperationException("Application is not initialized.");

        if (app.TryFindResource(resourceKey, app.ActualThemeVariant, out var value) &&
            value is Geometry geometry)
        {
            return geometry;
        }

        throw new InvalidOperationException($"Icon resource '{resourceKey}' was not found.");
    }

    public static void SetForeground(Control icon, IBrush? foreground)
    {
        if (icon is Viewbox { Child: Path path })
        {
            path.Stroke = foreground;
        }
    }
}
