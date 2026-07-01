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
        // Section header
        var header = new TextBlock
        {
            Text = "TOWNHALL",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondary"],
            Margin = new Thickness(12, 10, 12, 8)
        };

        _messagesPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 12,
            Margin = new Thickness(12, 8, 12, 12)
        };

        _scrollViewer = new ScrollViewer
        {
            Content = _messagesPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        var separator = new Border
        {
            Height = 1,
            Background = (IBrush?)Application.Current!.Resources["SurfaceBorder"]
        };

        Background = (IBrush?)Application.Current!.Resources["SurfaceBase"];

        Content = new DockPanel
        {
            Children =
            {
                new Border { Child = header, [DockPanel.DockProperty] = Dock.Top },
                separator,
                _scrollViewer
            }
        };

        this.GetObservable(MessagesProperty).Subscribe(_ => RenderMessages(), _ => { }, () => { });
    }

    private void RenderMessages()
    {
        _messagesPanel.Children.Clear();
        if (Messages is null)
            return;

        foreach (var message in Messages)
        {
            // Sender avatar circle (first letter)
            var avatarLetter = message.SenderId.Length > 0 ? message.SenderId[0].ToString() : "?";
            var avatar = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Background = (IBrush?)Application.Current!.Resources["ActiveHighlight"],
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock
                {
                    Text = avatarLetter,
                    FontSize = 13,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = (IBrush?)Application.Current!.Resources["SoftAccent"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            // Sender name
            var sender = new TextBlock
            {
                Text = message.SenderId,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = (IBrush?)Application.Current!.Resources["TextActive"],
                VerticalAlignment = VerticalAlignment.Center
            };

            // Timestamp
            var timestamp = new TextBlock
            {
                Text = message.Timestamp.ToLocalTime().ToString("HH:mm"),
                FontSize = 10,
                Foreground = (IBrush?)Application.Current!.Resources["TextSecondary"],
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };

            // Header row: sender + timestamp
            var headerRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 0,
                Children = { sender, timestamp }
            };

            // Message content
            var content = new TextBlock
            {
                Text = message.Content,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (IBrush?)Application.Current!.Resources["TextContent"],
                Margin = new Thickness(0, 4, 0, 0)
            };

            // Text column (name + content)
            var textColumn = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 0,
                Children = { headerRow, content }
            };

            // Message row: avatar + text
            var messageRow = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                }
            };
            Grid.SetColumn(avatar, 0);
            Grid.SetColumn(textColumn, 1);
            messageRow.Children.Add(avatar);
            messageRow.Children.Add(textColumn);

            _messagesPanel.Children.Add(messageRow);
        }

        _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, double.MaxValue);
    }
}