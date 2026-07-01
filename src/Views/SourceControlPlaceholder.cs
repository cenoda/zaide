using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Zaide.Views;

public class SourceControlPlaceholder : UserControl
{
    public SourceControlPlaceholder()
    {
        var header = new TextBlock
        {
            Text = "SOURCE CONTROL",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = (IBrush?)Application.Current!.Resources["TextActive"],
            Margin = new Thickness(12, 10, 12, 8)
        };

        var bodyText = new TextBlock
        {
            Text = "Placeholder (behavior deferred in Refactor 3)",
            FontSize = 12,
            Foreground = (IBrush?)Application.Current!.Resources["SoftAccent"],
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var body = new Border
        {
            Background = (IBrush?)Application.Current!.Resources["SurfacePanel"],
            BorderBrush = (IBrush?)Application.Current!.Resources["SurfaceBorder"],
            BorderThickness = new Thickness(1),
            Margin = new Thickness(12, 0, 12, 12),
            CornerRadius = new CornerRadius(6),
            Child = bodyText
        };

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            },
            Background = (IBrush?)Application.Current!.Resources["PanelDeep"]
        };

        Grid.SetRow(header, 0);
        Grid.SetRow(body, 1);
        root.Children.Add(header);
        root.Children.Add(body);

        Content = root;
    }
}
