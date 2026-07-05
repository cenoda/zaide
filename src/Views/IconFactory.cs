using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

namespace Zaide.Views;

public static class IconFactory
{
    public static Viewbox Create(string resourceKey, IBrush? foreground, double size = 16)
    {
        var path = new Path
        {
            Data = (Geometry)Application.Current!.Resources[resourceKey]!,
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

    public static void SetForeground(Control icon, IBrush? foreground)
    {
        if (icon is Viewbox { Child: Path path })
        {
            path.Fill = foreground;
        }
    }
}
