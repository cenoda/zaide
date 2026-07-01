using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Zaide.Models;

namespace Zaide.Views;

public class TownhallChatPanel : UserControl
{
    private readonly StackPanel _messagesPanel;
    private readonly ScrollViewer _scrollViewer;

    public static readonly StyledProperty<IReadOnlyList<TownhallMessage>?> MessagesProperty =
        AvaloniaProperty.Register<TownhallChatPanel, IReadOnlyList<TownhallMessage>?>(nameof(Messages));

    public IReadOnlyList<TownhallMessage>? Messages
    {
        get => GetValue(MessagesProperty);
        set => SetValue(MessagesProperty, value);
    }

    public TownhallChatPanel()
    {
        var header = new TextBlock
        {
            Text = "TOWNHALL",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = (IBrush?)Application.Current!.Resources["TextActive"],
            Margin = new Thickness(12, 10, 12, 8)
        };

        _messagesPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8,
            Margin = new Thickness(12, 0, 12, 12)
        };

        _scrollViewer = new ScrollViewer
        {
            Content = _messagesPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            },
            Background = (IBrush?)Application.Current!.Resources["SurfaceBase"]
        };

        Grid.SetRow(header, 0);
        Grid.SetRow(_scrollViewer, 1);
        root.Children.Add(header);
        root.Children.Add(_scrollViewer);

        Content = root;

        this.GetObservable(MessagesProperty).Subscribe(_ => RenderMessages(), _ => { }, () => { });
    }

    public void Refresh()
    {
        RenderMessages();
    }

    private void RenderMessages()
    {
        _messagesPanel.Children.Clear();
        if (Messages is null)
            return;

        foreach (var message in Messages)
        {
            var sender = new TextBlock
            {
                Text = message.SenderId,
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                Foreground = (IBrush?)Application.Current!.Resources["SoftAccent"]
            };

            var content = new TextBlock
            {
                Text = message.Content,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (IBrush?)Application.Current!.Resources["TextActive"]
            };

            var timestamp = new TextBlock
            {
                Text = message.Timestamp.ToLocalTime().ToString("HH:mm"),
                FontSize = 10,
                Foreground = (IBrush?)Application.Current!.Resources["SoftAccent"],
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var card = new Border
            {
                Background = (IBrush?)Application.Current!.Resources["SurfacePanel"],
                BorderBrush = (IBrush?)Application.Current!.Resources["SurfaceBorder"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Child = new StackPanel
                {
                    Spacing = 4,
                    Children = { sender, content, timestamp }
                }
            };

            _messagesPanel.Children.Add(card);
        }

        _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, double.MaxValue);
    }
}
