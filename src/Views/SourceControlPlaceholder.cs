using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Zaide.Views;

public class SourceControlPlaceholder : UserControl
{
    public SourceControlPlaceholder()
    {
        // Section header: "SOURCE CONTROL" + close button
        var sectionLabel = new TextBlock
        {
            Text = "SOURCE CONTROL",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondary"],
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };

        var closeButton = new TextBlock
        {
            Text = "×",
            FontSize = 14,
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondary"],
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        var headerRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Height = 32,
            Background = Brushes.Transparent
        };
        Grid.SetColumn(sectionLabel, 0);
        Grid.SetColumn(closeButton, 1);
        headerRow.Children.Add(sectionLabel);
        headerRow.Children.Add(closeButton);

        // Placeholder body
        var bodyText = new TextBlock
        {
            Text = "Placeholder\n(behavior deferred in Refactor 3)",
            FontSize = 12,
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondary"],
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };

        var separator = new Border
        {
            Height = 1,
            Background = (IBrush?)Application.Current!.Resources["SurfaceBorder"]
        };

        Background = (IBrush?)Application.Current!.Resources["PanelDeep"];

        Content = new DockPanel
        {
            Children =
            {
                new Border { Child = headerRow, [DockPanel.DockProperty] = Dock.Top },
                separator,
                new Border
                {
                    Child = bodyText,
                    [DockPanel.DockProperty] = Dock.Top,
                    Margin = new Thickness(12, 16, 12, 12)
                }
            }
        };
    }
}