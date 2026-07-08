using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using Zaide.Models;
using Zaide.Styles;
using Zaide.ViewModels;

namespace Zaide.Views;

/// <summary>
/// Composite Townhall view: the center column of the main window.
/// Internal structure:
/// - Left vertical sidebar (~140px): People panel (top) and Channels panel (bottom)
/// - Right: Chat message area and input area
/// Matches M3 spec and M0.5 palette.
/// </summary>
public class TownhallView : Panel, IDisposable
{
    private readonly TownhallPeoplePanel _peoplePanel;
    private readonly TownhallChannelPanel _channelPanel;
    private readonly TownhallChatPanel _chatPanel;
    private readonly TownhallInputArea _inputArea;
    private readonly ToggleButton _filterAllButton;
    private readonly ToggleButton _filterChatButton;
    private readonly ToggleButton _filterActivityButton;
    private CompositeDisposable? _disposables;

    /// <summary>
    /// Gets or sets the ViewModel. When set, wires all reactive bindings.
    /// </summary>
    public TownhallViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            _viewModel = value;
            WireViewModel();
        }
    }
    private TownhallViewModel? _viewModel;

    public TownhallView()
    {
        // --- Left sidebar (people + channels) ---
        _peoplePanel = new TownhallPeoplePanel
        {
            Background = (IBrush?)Application.Current!.Resources["SurfacePanelBrush"]
        };

        _channelPanel = new TownhallChannelPanel
        {
            Background = (IBrush?)Application.Current!.Resources["SurfacePanelBrush"]
        };

        // Sidebar: people (top) | channels (bottom)
        // M1.4: Removed 1px Border separator; panels are separated by background
        // contrast against the SurfaceBaseBrush window background instead.
        // The interactive GridSplitter between sections is preserved.
        var sidebar = new Grid
        {
            MinWidth = 100,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            },
            Children =
            {
                _peoplePanel,
                _channelPanel
            }
        };
        Grid.SetRow(_channelPanel, 2);

        // GridSplitter between people and channels sections (preserved — M1.4 only removes the 1px Border separator, not the interactive splitter)
        var sidebarSplitter = new GridSplitter
        {
            Height = 4,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.SizeNorthSouth),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeDirection = GridResizeDirection.Rows
        };
        Grid.SetRow(sidebarSplitter, 1);
        sidebar.Children.Add(sidebarSplitter);

        // --- Right side: chat + input ---
        _chatPanel = new TownhallChatPanel
        {
            Background = (IBrush?)Application.Current!.Resources["SurfacePanelBrush"]
        };

        _inputArea = new TownhallInputArea
        {
            Background = (IBrush?)Application.Current!.Resources["SurfacePanelBrush"]
        };

        // Segmented filter toggle (All / Chat / Activity) - placed above chat panel per M3
        _filterAllButton = new ToggleButton { Content = TextStyles.Caption("All"), IsChecked = true };
        _filterChatButton = new ToggleButton { Content = TextStyles.Caption("Chat") };
        _filterActivityButton = new ToggleButton { Content = TextStyles.Caption("Activity") };
        var filterGroup = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingXs,
            Margin = LayoutTokens.Inset(0, 0, 0, LayoutTokens.SpacingSm),
            Children = { _filterAllButton, _filterChatButton, _filterActivityButton }
        };

        var inputSeparator = new Border
        {
            Height = 1,
            Background = (IBrush?)Application.Current!.Resources["SeparatorBrush"],
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // Chat area: filter (auto) | chat messages (fills) | separator | input area (auto)
        var chatArea = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            Children =
            {
                filterGroup,
                _chatPanel,
                inputSeparator,
                _inputArea
            }
        };
        Grid.SetRow(filterGroup, 0);
        Grid.SetRow(_chatPanel, 1);
        Grid.SetRow(inputSeparator, 2);
        Grid.SetRow(_inputArea, 3);

        // GridSplitter between sidebar and chat area
        var sidebarChatSplitter = new GridSplitter
        {
            Width = 4,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.SizeWestEast),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        // Main layout: sidebar | splitter | chat area
        var mainGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(140), MinWidth = 100 },
                new ColumnDefinition { Width = new GridLength(4, GridUnitType.Pixel) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
            Children =
            {
                sidebar,
                sidebarChatSplitter,
                chatArea
            }
        };
        sidebarChatSplitter.DragCompleted += (_, _) =>
            GridLayoutResizeHelper.PreservePixelColumnAndNormalizeStarColumns(mainGrid, 0, 2);
        Grid.SetColumn(sidebarChatSplitter, 1);
        Grid.SetColumn(chatArea, 2);

        var outerBorder = new Border
        {
            Background = (IBrush?)Application.Current!.Resources["SurfaceBaseBrush"],
            // M5-allow: M1 introduced the 1px left seam so the Townhall surface stays visually separated.
            Padding = LayoutTokens.Inset(1, 0, 0, 0),
            Child = mainGrid
        };

        Children.Add(outerBorder);

        // Wire input area send event
        _inputArea.SendRequested += OnSendRequested;
    }

    private void OnSendRequested()
    {
        if (_viewModel is null) return;

        // Sync text from input to ViewModel draft, then send
        _viewModel.DraftText = _inputArea.InputText;
        _viewModel.SendMessageCommand.Execute().Subscribe();
    }

    private void WireViewModel()
    {
        _disposables?.Dispose();
        _disposables = new CompositeDisposable();

        if (_viewModel is null) return;

        // Populate people panel
        _peoplePanel.SetAgents(_viewModel.Agents);

        // Populate channel panel
        _channelPanel.SetOnChannelSelected(channelId =>
        {
            _viewModel.SelectChannelCommand.Execute(channelId).Subscribe();
        });
        _channelPanel.SetChannels(_viewModel.Channels);

        // Populate chat panel with initial messages (will be updated by FilteredMessages subscription below)
        if (_viewModel.Messages is not null)
        {
            _chatPanel.SetMessages(new ObservableCollection<TownhallMessage>(_viewModel.Messages));
        }

        // Update placeholder text based on active channel
        UpdatePlaceholder();

        // React to active channel changes: update channel list highlight and messages
        _disposables.Add(
            _viewModel.WhenAnyValue(x => x.ActiveChannelId)
                .Subscribe(_ =>
                {
                    _channelPanel.SetChannels(_viewModel.Channels);
                    UpdatePlaceholder();
                }));

        // React to FilteredMessages changes (filter mode or underlying collection updates).
        _disposables.Add(
            _viewModel.FilteredMessages
                .Subscribe(filtered =>
                {
                    var oc = new ObservableCollection<TownhallMessage>(filtered);
                    _chatPanel.SetMessages(oc);
                }));

        // Wire filter toggle buttons to FilterMode (using Avalonia.Interactivity.RoutedEventArgs for IsCheckedChanged).
        // ToggleButton has no built-in mutual-exclusivity (unlike RadioButton with GroupName), so each handler
        // explicitly unchecks the other two buttons when checked, guarding against redundant sets to avoid
        // re-entrant event storms.
        _disposables.Add(
            Observable.FromEventPattern<Avalonia.Interactivity.RoutedEventArgs>(h => _filterAllButton.IsCheckedChanged += h, h => _filterAllButton.IsCheckedChanged -= h)
                .Subscribe(_ =>
                {
                    if (_filterAllButton.IsChecked != true) return;
                    _viewModel.FilterMode = FilterMode.All;
                    if (_filterChatButton.IsChecked != false) _filterChatButton.IsChecked = false;
                    if (_filterActivityButton.IsChecked != false) _filterActivityButton.IsChecked = false;
                }));
        _disposables.Add(
            Observable.FromEventPattern<Avalonia.Interactivity.RoutedEventArgs>(h => _filterChatButton.IsCheckedChanged += h, h => _filterChatButton.IsCheckedChanged -= h)
                .Subscribe(_ =>
                {
                    if (_filterChatButton.IsChecked != true) return;
                    _viewModel.FilterMode = FilterMode.ChatOnly;
                    if (_filterAllButton.IsChecked != false) _filterAllButton.IsChecked = false;
                    if (_filterActivityButton.IsChecked != false) _filterActivityButton.IsChecked = false;
                }));
        _disposables.Add(
            Observable.FromEventPattern<Avalonia.Interactivity.RoutedEventArgs>(h => _filterActivityButton.IsCheckedChanged += h, h => _filterActivityButton.IsCheckedChanged -= h)
                .Subscribe(_ =>
                {
                    if (_filterActivityButton.IsChecked != true) return;
                    _viewModel.FilterMode = FilterMode.ActivityOnly;
                    if (_filterAllButton.IsChecked != false) _filterAllButton.IsChecked = false;
                    if (_filterChatButton.IsChecked != false) _filterChatButton.IsChecked = false;
                }));

        // Sync draft changes: when ViewModel draft changes (e.g., cleared after send), update input
        _disposables.Add(
            _viewModel.WhenAnyValue(x => x.DraftText)
                .Subscribe(draft =>
                {
                    _inputArea.InputText = draft;
                }));

        // Wire input TextChanged to push back to ViewModel for bidirectional draft sync
        _disposables.Add(
            Observable.FromEventPattern(
                h => _inputArea.TextChanged += h,
                h => _inputArea.TextChanged -= h)
            .Subscribe(_ =>
            {
                if (_viewModel is not null)
                    _viewModel.DraftText = _inputArea.InputText;
            }));
    }

    private void UpdatePlaceholder()
    {
        if (_viewModel?.ActiveChannelId is not null)
        {
            var activeChannel = _viewModel.Channels.FirstOrDefault(c => c.IsActive);
            if (activeChannel is not null)
            {
                _inputArea.PlaceholderText = $"Message #{activeChannel.Name}";
                return;
            }
        }
        _inputArea.PlaceholderText = "Message...";
    }

    public void Dispose()
    {
        _disposables?.Dispose();
        _disposables = null;
    }
}
