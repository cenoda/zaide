using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.ObjectModel;
using Zaide.Models;
using Zaide.Styles;

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
            Spacing = LayoutTokens.SpacingNone
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

        TownhallMessage? previousMessage = null;
        foreach (var message in messages)
        {
            var renderHeader = ShouldRenderHeader(previousMessage, message);
            _messageList.Children.Add(CreateMessageRow(message, renderHeader));
            previousMessage = message;
        }

        // Scroll to bottom (newest messages)
        _scrollViewer.ScrollToEnd();
    }

    private static bool ShouldRenderHeader(TownhallMessage? previousMessage, TownhallMessage message)
    {
        if (previousMessage is null)
        {
            return true;
        }

        if (!string.Equals(message.SenderId, previousMessage.SenderId, StringComparison.Ordinal))
        {
            return true;
        }

        var timeGap = message.Timestamp - previousMessage.Timestamp;
        return timeGap < TimeSpan.Zero || timeGap >= TimeSpan.FromMinutes(5);
    }

    private static Border CreateMessageRow(TownhallMessage message, bool renderHeader)
    {
        var messageContainer = new StackPanel
        {
            Orientation = Orientation.Vertical
        };

        if (renderHeader)
        {
            var avatar = TownhallAvatarFactory.Create(message.SenderName, "SuccessBrush", 28, 8);

            var senderName = TextStyles.Header(message.SenderName);
            senderName.VerticalAlignment = VerticalAlignment.Center;

            var timestamp = TextStyles.Caption(FormatTimestamp(message.Timestamp));
            timestamp.VerticalAlignment = VerticalAlignment.Center;
            timestamp.Margin = LayoutTokens.Inset(LayoutTokens.SpacingSm - LayoutTokens.SpacingXxs, 0, 0, 0);

            var senderMeta = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { senderName, timestamp }
            };

            var headerRow = new Grid
            {
                Name = "MessageHeaderRow",
                ColumnDefinitions =
                {
                new ColumnDefinition { Width = new GridLength(28) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
                Margin = LayoutTokens.Inset(0, 0, 0, LayoutTokens.SpacingXxs),
                Children = { avatar, senderMeta }
            };
            Grid.SetColumn(senderMeta, 1);

            messageContainer.Children.Add(headerRow);
        }

        // Message content
        var contentText = TextStyles.Body(message.Content);
        contentText.TextWrapping = TextWrapping.Wrap;

        // Warning icon for warning messages
        StackPanel contentStack;
        if (message.Kind == TownhallMessageKind.System)
        {
            var warningIcon = IconFactory.Create(
                "Icon.Warning",
                (IBrush?)Application.Current!.Resources["WarningBrush"],
                14);
            warningIcon.VerticalAlignment = VerticalAlignment.Top;
            warningIcon.Margin = LayoutTokens.Inset(0, 0, LayoutTokens.SpacingXs, 0);

            contentStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = LayoutTokens.SpacingXs,
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
        contentStack.Margin = LayoutTokens.Inset(
            LayoutTokens.SpacingXxl + LayoutTokens.SpacingMd,
            0,
            0,
            0);
        messageContainer.Children.Add(contentStack);

        var row = new Border
        {
            Padding = renderHeader
                ? LayoutTokens.Inset(LayoutTokens.SpacingLg, LayoutTokens.SpacingMd, LayoutTokens.SpacingLg, LayoutTokens.SpacingXs)
                : LayoutTokens.Inset(LayoutTokens.SpacingLg, LayoutTokens.SpacingXxs, LayoutTokens.SpacingLg, LayoutTokens.SpacingXs),
            Child = messageContainer
        };

        // Warning message background tint
        if (message.Kind == TownhallMessageKind.System)
        {
            row.Background = new SolidColorBrush(Color.FromArgb(0x15, 0xFC, 0xBB, 0x47));
            row.CornerRadius = LayoutTokens.RadiusMd;
        }

        return row;
    }

    private static string FormatTimestamp(DateTimeOffset timestamp)
    {
        var localTimestamp = timestamp.ToLocalTime();
        return localTimestamp.Date == DateTimeOffset.Now.Date
            ? localTimestamp.ToString("HH:mm")
            : localTimestamp.ToString("MMM d HH:mm");
    }
}
