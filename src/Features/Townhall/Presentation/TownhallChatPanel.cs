using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Zaide.Features.Townhall.Domain;
using Zaide.UI.DesignSystem;
using Zaide.App.Shell;

namespace Zaide.Features.Townhall.Presentation;

/// <summary>
/// Chat message panel for the Townhall.
/// Shows the active conversation header, scrollable message list, sender avatars,
/// and a new-messages affordance when scrolled away.
/// Matches M0.5 palette and M3 spec.
/// </summary>
public class TownhallChatPanel : Panel
{
    private readonly Grid _root;
    private readonly TextBlock _conversationHeader;
    private readonly StackPanel _messageList;
    private readonly ScrollViewer _scrollViewer;
    private readonly Button _newMessagesButton;
    private int _renderedMessageCount;
    private string? _firstMessageId;
    private TownhallMessage? _previousMessage;
    private int _unseenMessageCount;

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
        _scrollViewer.ScrollChanged += OnScrollChanged;

        _newMessagesButton = new Button
        {
            Content = TextStyles.Caption("New messages"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = LayoutTokens.Inset(0, 0, 0, LayoutTokens.SpacingMd),
            IsVisible = false
        };
        AutomationProperties.SetName(_newMessagesButton, "Jump to new messages");
        AutomationProperties.SetHelpText(
            _newMessagesButton,
            "Scroll to the latest messages in this conversation.");
        _newMessagesButton.Click += (_, _) => ScrollToEndAndClearChip();

        _conversationHeader = TextStyles.Header(string.Empty);
        _conversationHeader.IsVisible = false;
        AutomationProperties.SetName(_conversationHeader, "Active conversation");
        var headerBorder = new Border
        {
            Padding = LayoutTokens.Inset(
                LayoutTokens.SpacingLg,
                LayoutTokens.SpacingMd,
                LayoutTokens.SpacingLg,
                LayoutTokens.SpacingSm),
            Child = _conversationHeader
        };

        var messageArea = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            },
            Children = { headerBorder, _scrollViewer }
        };
        Grid.SetRow(_scrollViewer, 1);

        _root = new Grid
        {
            Children = { messageArea, _newMessagesButton }
        };

        Children.Add(_root);
    }

    /// <summary>
    /// Gets the rendered conversation header label (for tests).
    /// </summary>
    public string ConversationHeaderLabel => _conversationHeader.Text ?? string.Empty;

    /// <summary>
    /// Updates the conversation header shown above the message list.
    /// </summary>
    public void SetConversationHeader(string label)
    {
        _conversationHeader.Text = label;
        _conversationHeader.IsVisible = !string.IsNullOrEmpty(label);
    }

    /// <summary>
    /// Clears rendered rows when the authoritative conversation selection changes.
    /// </summary>
    public void ResetForConversation()
    {
        _messageList.Children.Clear();
        _renderedMessageCount = 0;
        _firstMessageId = null;
        _previousMessage = null;
        _unseenMessageCount = 0;
        HideNewMessageChip();
    }

    /// <summary>
    /// Populates the message list. Called when messages change (channel switch or new message).
    /// Scrolls to bottom after population.
    /// </summary>
    public void SetMessages(ObservableCollection<TownhallMessage> messages)
    {
        ResetForConversation();
        UpdateMessages(messages);
        ScrollToEnd();
    }

    /// <summary>
    /// Applies filtered message updates with scroll anchoring and near-bottom auto-follow.
    /// </summary>
    public void UpdateMessages(IReadOnlyList<TownhallMessage> messages)
    {
        if (messages.Count < _renderedMessageCount || NeedsFullRebuild(messages))
        {
            var scrollToEnd = _renderedMessageCount == 0 || IsNearBottom();
            RebuildAll(messages);
            if (scrollToEnd)
            {
                ScrollToEnd();
            }

            return;
        }

        if (messages.Count == _renderedMessageCount)
        {
            return;
        }

        var wasNearBottom = IsNearBottom();
        var appendedCount = 0;
        for (var i = _renderedMessageCount; i < messages.Count; i++)
        {
            var message = messages[i];
            var renderHeader = ShouldRenderHeader(_previousMessage, message);
            _messageList.Children.Add(CreateMessageRow(message, renderHeader));
            _previousMessage = message;
            appendedCount++;
        }

        _renderedMessageCount = messages.Count;
        if (_firstMessageId is null && messages.Count > 0)
        {
            _firstMessageId = messages[0].Id;
        }

        if (TownhallChatScrollPolicy.ShouldAutoFollowOnAppend(wasNearBottom))
        {
            ScrollToEnd();
            return;
        }

        if (appendedCount > 0)
        {
            _unseenMessageCount += appendedCount;
            ShowNewMessageChip();
        }
    }

    private bool NeedsFullRebuild(IReadOnlyList<TownhallMessage> messages)
    {
        if (_renderedMessageCount == 0)
        {
            return false;
        }

        if (messages.Count == 0)
        {
            return _renderedMessageCount > 0;
        }

        return !string.Equals(messages[0].Id, _firstMessageId, StringComparison.Ordinal);
    }

    private void RebuildAll(IReadOnlyList<TownhallMessage> messages)
    {
        _messageList.Children.Clear();
        _renderedMessageCount = 0;
        _firstMessageId = null;
        _previousMessage = null;
        _unseenMessageCount = 0;
        HideNewMessageChip();

        foreach (var message in messages)
        {
            var renderHeader = ShouldRenderHeader(_previousMessage, message);
            _messageList.Children.Add(CreateMessageRow(message, renderHeader));
            _previousMessage = message;
            _renderedMessageCount++;
        }

        if (messages.Count > 0)
        {
            _firstMessageId = messages[0].Id;
        }
    }

    private bool IsNearBottom() =>
        TownhallChatScrollPolicy.IsNearBottom(
            _scrollViewer.Offset.Y,
            _scrollViewer.ScrollBarMaximum.Y);

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (IsNearBottom() && _unseenMessageCount > 0)
        {
            ScrollToEndAndClearChip();
        }
    }

    private void ScrollToEnd()
    {
        _scrollViewer.ScrollToEnd();
        HideNewMessageChip();
    }

    private void ScrollToEndAndClearChip()
    {
        _unseenMessageCount = 0;
        ScrollToEnd();
    }

    private void ShowNewMessageChip()
    {
        _newMessagesButton.IsVisible = true;
        var label = _unseenMessageCount == 1
            ? "New message"
            : $"New messages ({_unseenMessageCount})";
        _newMessagesButton.Content = TextStyles.Caption(label);
        AutomationProperties.SetName(_newMessagesButton, $"Jump to {label.ToLowerInvariant()}");
    }

    private void HideNewMessageChip()
    {
        _unseenMessageCount = 0;
        _newMessagesButton.IsVisible = false;
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
