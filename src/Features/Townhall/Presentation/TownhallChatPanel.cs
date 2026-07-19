using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.ObjectModel;
using Zaide.Features.Townhall.Domain;
using Zaide.UI.DesignSystem;
using Zaide.App.Shell;

namespace Zaide.Features.Townhall.Presentation;

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
        if (message.Kind == TownhallMessageKind.Chat)
        {
            return CreateChatMessageRow(message, renderHeader);
        }

        return CreateCompactMessageRow(message);
    }

    private static Border CreateChatMessageRow(TownhallMessage message, bool renderHeader)
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

        var contentStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { contentText }
        };

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

        return row;
    }

    /// <summary>
    /// Creates a compact single-line row for non-Chat activity entries (log style).
    /// Uses theme tokens only per DESIGN.md §8. One generic icon for all non-Chat kinds (YAGNI).
    /// </summary>
    private static Border CreateCompactMessageRow(TownhallMessage message)
    {
        var iconBrush = message.Kind == TownhallMessageKind.System
            ? PaletteTokens.WarningBrush
            : PaletteTokens.TextSecondaryBrush;
        var icon = IconFactory.Create("Icon.Info", iconBrush, 12);
        icon.VerticalAlignment = VerticalAlignment.Center;
        icon.Margin = LayoutTokens.Inset(0, 0, LayoutTokens.SpacingXs, 0);

        var timestamp = TextStyles.Caption(FormatTimestamp(message.Timestamp));
        timestamp.VerticalAlignment = VerticalAlignment.Center;
        timestamp.Foreground = PaletteTokens.TextSecondaryBrush;

        var summary = TextStyles.Caption(message.Content);
        summary.VerticalAlignment = VerticalAlignment.Center;
        summary.Foreground = PaletteTokens.TextSecondaryBrush;
        summary.TextTrimming = TextTrimming.CharacterEllipsis;

        var rowContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingXs,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { icon, timestamp, summary }
        };

        var row = new Border
        {
            Padding = LayoutTokens.Inset(LayoutTokens.SpacingXs, LayoutTokens.SpacingXxs, LayoutTokens.SpacingXs, LayoutTokens.SpacingXxs),
            Background = PaletteTokens.SurfacePanelBrush,
            Child = rowContent
        };

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
