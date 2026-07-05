using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Zaide.Views;

public static class IconFactory
{
    public static PathIcon Create(string resourceKey, IBrush? foreground, double size = 16)
    {
        return new PathIcon
        {
            Data = (Geometry)Application.Current!.Resources[resourceKey]!,
            Width = size,
            Height = size,
            Foreground = foreground,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }
}
