using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.ObjectModel;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Townhall.Domain;
using Zaide.UI.DesignSystem;
using Zaide.App.Shell;

namespace Zaide.Features.Townhall.Presentation;

/// <summary>
/// Sidebar navigation for Townhall channels and direct conversations.
/// Supports pointer and keyboard selection with accessible list names.
/// </summary>
internal sealed class TownhallNavigationPanel : Panel
{
    private static readonly Color ActiveRowOverlay = Color.FromArgb(0x15, 0x06, 0x6A, 0xDB);
    private static readonly Color HoverOverlay = Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF);

    private readonly ListBox _channelList;
    private readonly ListBox _directList;
    private Action<string>? _onChannelSelected;
    private Action<ConversationId>? _onDirectSelected;

    public TownhallNavigationPanel()
    {
        var channelsHeader = TextStyles.Header("Channels");
        channelsHeader.Margin = LayoutTokens.Inset(LayoutTokens.SpacingMd, LayoutTokens.SpacingMd, LayoutTokens.SpacingMd, LayoutTokens.SpacingXs);

        _channelList = CreateNavListBox("Townhall channels");
        _channelList.SelectionChanged += OnChannelSelectionChanged;
        _channelList.KeyDown += OnChannelListKeyDown;

        var directsHeader = TextStyles.Header("Direct");
        directsHeader.Margin = LayoutTokens.Inset(LayoutTokens.SpacingMd, LayoutTokens.SpacingLg, LayoutTokens.SpacingMd, LayoutTokens.SpacingXs);

        _directList = CreateNavListBox("Townhall direct conversations");
        _directList.SelectionChanged += OnDirectSelectionChanged;
        _directList.KeyDown += OnDirectListKeyDown;

        var scrollContent = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = LayoutTokens.SpacingNone,
            Children =
            {
                channelsHeader,
                _channelList,
                directsHeader,
                _directList
            }
        };

        var scrollViewer = new ScrollViewer
        {
            Content = scrollContent,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        Children.Add(scrollViewer);
    }

    public void SetOnChannelSelected(Action<string> onSelected)
    {
        _onChannelSelected = onSelected;
    }

    public void SetOnDirectSelected(Action<ConversationId> onSelected)
    {
        _onDirectSelected = onSelected;
    }

    public void SetChannels(ObservableCollection<Channel> channels)
    {
        // Keep the same ItemsSource instance when possible so intermediate
        // CollectionChanged events during rebuild do not rebind templates.
        if (!ReferenceEquals(_channelList.ItemsSource, channels))
        {
            _channelList.ItemsSource = channels;
        }

        _channelList.ItemTemplate ??= new Avalonia.Controls.Templates.FuncDataTemplate<Channel>(
            (channel, _) => channel is null
                ? new Border()
                : CreateChannelRow(channel));
    }

    public void SetDirectItems(ObservableCollection<TownhallNavigationItem> items)
    {
        if (!ReferenceEquals(_directList.ItemsSource, items))
        {
            _directList.ItemsSource = items;
        }

        // Null item guard: ListBox can invoke the template with null while the
        // bound ObservableCollection is mid-rebuild (Clear before re-Add).
        // An uncaught NRE here was previously surfaced as an agent ExecutionFailure.
        _directList.ItemTemplate ??= new Avalonia.Controls.Templates.FuncDataTemplate<TownhallNavigationItem>(
            (item, _) => item is null
                ? new Border()
                : CreateDirectRow(item));
    }

    private static ListBox CreateNavListBox(string automationName)
    {
        var list = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = LayoutTokens.NoneThickness,
            Margin = LayoutTokens.Inset(0, 0, 0, LayoutTokens.SpacingSm),
            Focusable = true
        };
        AutomationProperties.SetName(list, automationName);
        AutomationProperties.SetHelpText(
            list,
            "Use arrow keys to move focus. Press Enter or Space to select.");
        return list;
    }

    private Border CreateChannelRow(Channel channel)
    {
        var nameText = TextStyles.Body($"#{channel.Name}");
        nameText.VerticalAlignment = VerticalAlignment.Center;
        ApplyRowForeground(nameText, channel.IsActive);

        var unreadDot = CreateUnreadDot(channel.HasUnread);

        var contentStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingXs,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { unreadDot, nameText }
        };

        if (channel.IsPinned)
        {
            var pinIcon = IconFactory.Create(
                "Icon.Pin",
                PaletteTokens.TextSecondaryBrush,
                12);
            pinIcon.Margin = LayoutTokens.Inset(LayoutTokens.SpacingXs, 0, 0, 0);
            contentStack.Children.Add(pinIcon);
        }

        var row = CreateSelectableRow(contentStack, channel.IsActive);
        row.PointerPressed += (_, _) => _onChannelSelected?.Invoke(channel.Id);

        void OnChannelPropertyChanged(object? _, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Channel.IsActive))
            {
                ApplyRowForeground(nameText, channel.IsActive);
                ApplyRowActiveBackground(row, channel.IsActive);
            }
            else if (e.PropertyName == nameof(Channel.HasUnread))
            {
                unreadDot.IsVisible = channel.HasUnread;
            }
        }

        channel.PropertyChanged += OnChannelPropertyChanged;
        row.DetachedFromVisualTree += (_, _) => channel.PropertyChanged -= OnChannelPropertyChanged;
        return row;
    }

    private Border CreateDirectRow(TownhallNavigationItem item)
    {
        var nameText = TextStyles.Body(item.Label);
        nameText.VerticalAlignment = VerticalAlignment.Center;
        ApplyRowForeground(nameText, item.IsSelected);

        var unreadDot = CreateUnreadDot(item.HasUnread);

        var contentStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingXs,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { unreadDot, nameText }
        };

        var row = CreateSelectableRow(contentStack, item.IsSelected);
        row.PointerPressed += (_, _) => _onDirectSelected?.Invoke(item.ConversationId);

        void OnItemPropertyChanged(object? _, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TownhallNavigationItem.IsSelected))
            {
                ApplyRowForeground(nameText, item.IsSelected);
                ApplyRowActiveBackground(row, item.IsSelected);
            }
            else if (e.PropertyName == nameof(TownhallNavigationItem.HasUnread))
            {
                unreadDot.IsVisible = item.HasUnread;
            }
        }

        item.PropertyChanged += OnItemPropertyChanged;
        row.DetachedFromVisualTree += (_, _) => item.PropertyChanged -= OnItemPropertyChanged;
        return row;
    }

    private static Ellipse CreateUnreadDot(bool isVisible)
    {
        const double size = 8d;
        return new Ellipse
        {
            Width = size,
            Height = size,
            Fill = PaletteTokens.PrimaryAccentBrush,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = isVisible
        };
    }

    private static void ApplyRowForeground(TextBlock nameText, bool isActive)
    {
        nameText.Foreground = isActive
            ? PaletteTokens.TextPrimaryBrush
            : PaletteTokens.TextSecondaryBrush;
        if (isActive)
        {
            nameText.FontWeight = FontWeight.SemiBold;
        }
        else
        {
            nameText.FontWeight = FontWeight.Normal;
        }
    }

    private static void ApplyRowActiveBackground(Border row, bool isActive)
    {
        if (isActive)
        {
            row.Background = new SolidColorBrush(ActiveRowOverlay);
            row.CornerRadius = LayoutTokens.RadiusSm;
        }
        else
        {
            row.Background = null;
            row.CornerRadius = LayoutTokens.NoneRadius;
        }
    }

    private static Border CreateSelectableRow(Control content, bool isActive)
    {
        var row = new Border
        {
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingMd, LayoutTokens.SpacingSm - LayoutTokens.SpacingXxs),
            Child = content,
            Cursor = new Cursor(StandardCursorType.Hand),
            Focusable = true
        };

        ApplyRowActiveBackground(row, isActive);

        row.PointerEntered += (_, _) =>
        {
            if (row.Background is null)
            {
                row.Background = new SolidColorBrush(HoverOverlay);
                row.CornerRadius = LayoutTokens.RadiusSm;
            }
        };
        row.PointerExited += (_, _) =>
        {
            // Restore active overlay if still selected; otherwise clear hover.
            // Active state is reapplied via PropertyChanged; hover only when no background.
            if (row.Background is SolidColorBrush brush
                && brush.Color == HoverOverlay)
            {
                row.Background = null;
                row.CornerRadius = LayoutTokens.NoneRadius;
            }
        };

        return row;
    }

    private void OnChannelSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_channelList.SelectedItem is Channel channel)
        {
            _onChannelSelected?.Invoke(channel.Id);
        }
    }

    private void OnDirectSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_directList.SelectedItem is TownhallNavigationItem item)
        {
            _onDirectSelected?.Invoke(item.ConversationId);
        }
    }

    private void OnChannelListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            if (_channelList.SelectedItem is Channel channel)
            {
                _onChannelSelected?.Invoke(channel.Id);
                e.Handled = true;
            }
        }
    }

    private void OnDirectListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            if (_directList.SelectedItem is TownhallNavigationItem item)
            {
                _onDirectSelected?.Invoke(item.ConversationId);
                e.Handled = true;
            }
        }
    }
}
