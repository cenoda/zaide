using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Zaide.Models;

namespace Zaide.Views;

/// <summary>
/// Chat message panel for the Townhall.
/// Shows a scrollable list of messages with sender avatar, name, content, and timestamp.
/// Warning-type messages show amber alert icon and tinted background.
/// Messages are displayed newest-at-bottom.
/// Matches M0.5 palette and M3 spec.
/// </summary>
public class TownhallChatPanel : Panel
{
    private readonly StackPanel _messageList;
    private readonly ScrollViewer _scrollViewer;

    public TownhallChatPanel()
    {
        _messageList = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0
        };

        _scrollViewer = new ScrollViewer
        {
            Content = _messageList,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        Children.Add(_scrollViewer);
    }

    /// <summary>
    /// Populates the message list. Called when messages change (channel switch or new message).
    /// Scrolls to bottom after population.
    /// </summary>
    public void SetMessages(ObservableCollection<TownhallMessage> messages)
    {
        _messageList.Children.Clear();

        foreach (var message in messages)
        {
            _messageList.Children.Add(CreateMessageRow(message));
        }

        // Scroll to bottom (newest messages)
        _scrollViewer.ScrollToEnd();
    }

    private static Border CreateMessageRow(TownhallMessage message)
    {
        // Avatar circle with initials
        var initials = message.SenderName.Length > 0
            ? message.SenderName[..1].ToUpperInvariant()
            : "?";

        var avatarCircle = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(9999),
            Background = (IBrush?)Application.Current!.Resources["PrimaryAccentBrush"],
            Child = new TextBlock
            {
                Text = initials,
                Foreground = (IBrush?)Application.Current!.Resources["TextPrimaryBrush"],
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        // Sender name
        var senderName = new TextBlock
        {
            Text = message.SenderName,
            Foreground = (IBrush?)Application.Current!.Resources["TextPrimaryBrush"],
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Timestamp
        var timestamp = new TextBlock
        {
            Text = message.Timestamp.ToLocalTime().ToString("HH:mm"),
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        // Header row: avatar + sender name + timestamp
        var headerRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(28) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Margin = new Thickness(0, 0, 0, 2),
            Children = { avatarCircle, senderName, timestamp }
        };
        Grid.SetColumn(senderName, 1);
        Grid.SetColumn(timestamp, 2);

        // Message content
        var contentText = new TextBlock
        {
            Text = message.Content,
            Foreground = (IBrush?)Application.Current!.Resources["TextPrimaryBrush"],
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap
        };

        // Warning icon for warning messages
        StackPanel contentStack;
        if (message.Type == TownhallMessageType.Warning)
        {
            var warningIcon = new TextBlock
            {
                Text = "\u26A0",
                FontSize = 14,
                Foreground = (IBrush?)Application.Current!.Resources["WarningBrush"],
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 4, 0)
            };

            contentStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Children = { warningIcon, contentText }
            };
        }
        else
        {
            contentStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { contentText }
            };
        }

        // Indent content past avatar
        contentStack.Margin = new Thickness(36, 0, 0, 0);

        var messageContainer = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Children = { headerRow, contentStack }
        };

        var row = new Border
        {
            Padding = new Thickness(16, 12, 16, 12),
            Child = messageContainer
        };

        // Warning message background tint
        if (message.Type == TownhallMessageType.Warning)
        {
            row.Background = new SolidColorBrush(Color.FromArgb(0x15, 0xFC, 0xBB, 0x47));
            row.CornerRadius = new CornerRadius(8);
        }

        return row;
    }
}