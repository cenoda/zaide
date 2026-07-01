using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Zaide.Models;

namespace Zaide.Views;

public class TownhallChannelPanel : UserControl
{
    private readonly StackPanel _listPanel;

    public static readonly StyledProperty<System.Collections.Generic.IReadOnlyList<Channel>?> ChannelsProperty =
        AvaloniaProperty.Register<TownhallChannelPanel, System.Collections.Generic.IReadOnlyList<Channel>?>(nameof(Channels));

    public System.Collections.Generic.IReadOnlyList<Channel>? Channels
    {
        get => GetValue(ChannelsProperty);
        set => SetValue(ChannelsProperty, value);
    }

    public event Action<string>? ChannelSelected;

    public TownhallChannelPanel()
    {
        var header = new TextBlock
        {
            Text = "CHANNELS",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = (IBrush?)Application.Current!.Resources["SoftAccent"],
            Margin = new Thickness(10, 10, 10, 8)
        };

        _listPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 4,
            Margin = new Thickness(6, 0, 6, 6)
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
        Grid.SetRow(_listPanel, 1);
        root.Children.Add(header);
        root.Children.Add(_listPanel);

        Content = root;

        this.GetObservable(ChannelsProperty).Subscribe(_ => RenderChannels());
    }

    private void RenderChannels()
    {
        _listPanel.Children.Clear();
        if (Channels is null)
            return;

        foreach (var channel in Channels)
        {
            var text = new TextBlock
            {
                Text = channel.Name,
                FontSize = 12,
                Foreground = (IBrush?)Application.Current!.Resources["TextActive"],
                VerticalAlignment = VerticalAlignment.Center
            };

            var row = new Border
            {
                Height = 28,
                Padding = new Thickness(8, 0, 8, 0),
                CornerRadius = new CornerRadius(6),
                Background = channel.IsActive
                    ? (IBrush?)Application.Current!.Resources["PrimaryAccent"]
                    : Brushes.Transparent,
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = text
            };

            var channelId = channel.Id;
            row.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(row).Properties.IsLeftButtonPressed)
                    return;

                ChannelSelected?.Invoke(channelId);
                e.Handled = true;
            };

            _listPanel.Children.Add(row);
        }
    }
}
