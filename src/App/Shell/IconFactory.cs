using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace Zaide.App.Shell;
public static class IconFactory
{
    public static Viewbox Create(string resourceKey, IBrush? foreground, double size = 16)
    {
        var geometry = ResolveIconGeometry(resourceKey);
        // Phosphor Regular geometries in Icons.axaml are closed fill-oriented paths.
        // Paint with Fill at header/toolbar sizes (~14–20px); stroke rendering turns
        // these glyphs into mushy, unreadable outlines.
        var path = new Path
        {
            Data = geometry,
            Width = 256,
            Height = 256,
            Fill = foreground,
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
        var app = Application.Current;
        if (app is null)
        {
            // Fallback for test environments where Application.Current is not
            // initialized with theme resources. A simple empty geometry avoids
            // crashing the test.
            return new StreamGeometry();
        }

        try
        {
            if (app.TryFindResource(resourceKey, ThemeVariant.Default, out var value) &&
                value is Geometry geometry)
            {
                return geometry;
            }
        }
        catch (InvalidOperationException)
        {
            return new StreamGeometry();
        }

        throw new InvalidOperationException($"Icon resource '{resourceKey}' was not found.");
    }

    public static void SetForeground(Control icon, IBrush? foreground)
    {
        if (icon is Viewbox { Child: Path path })
        {
            path.Fill = foreground;
        }
    }
}
