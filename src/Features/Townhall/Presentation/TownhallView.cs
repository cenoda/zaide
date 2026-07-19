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
using Zaide.Features.Townhall.Domain;
using Zaide.UI.DesignSystem;
using Zaide.App.Shell;

namespace Zaide.Features.Townhall.Presentation;

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
        _peoplePanel = new TownhallPeoplePanel { Background = PaletteTokens.SurfacePanelBrush };
        _channelPanel = new TownhallChannelPanel { Background = PaletteTokens.SurfacePanelBrush };
        _chatPanel = new TownhallChatPanel { Background = PaletteTokens.SurfacePanelBrush };
        _inputArea = new TownhallInputArea
        {
            Background = PaletteTokens.SurfacePanelBrush,
            VerticalAlignment = VerticalAlignment.Bottom
        };

        // Filter toggle buttons: All / Chat / Activity
        _filterAllButton = new ToggleButton { Content = TextStyles.Caption("All"), IsChecked = true };
        _filterChatButton = new ToggleButton { Content = TextStyles.Caption("Chat") };
        _filterActivityButton = new ToggleButton { Content = TextStyles.Caption("Activity") };

        var sidebar = BuildSidebar();
        var filterGroup = BuildFilterGroup();
        var chatArea = BuildChatArea(filterGroup);
        var mainGrid = BuildMainLayout(sidebar, chatArea);

        var outerBorder = new Border
        {
            Background = PaletteTokens.SurfaceBaseBrush,
            // M5-allow: M1 introduced the 1px left seam so the Townhall surface stays visually separated.
            Padding = LayoutTokens.Inset(1, 0, 0, 0),
            Child = mainGrid
        };

        Children.Add(outerBorder);

        // Wire input area send event
        _inputArea.SendRequested += OnSendRequested;
    }

    /// <summary>
    /// Builds the left sidebar: people panel (top) | interactive splitter | channels panel (bottom).
    /// </summary>
    private Grid BuildSidebar()
    {
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

        return sidebar;
    }

    /// <summary>
    /// Builds the filter toggle group: All / Chat / Activity buttons.
    /// </summary>
    private StackPanel BuildFilterGroup()
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingXs,
            Margin = LayoutTokens.Inset(0, 0, 0, LayoutTokens.SpacingSm),
            Children = { _filterAllButton, _filterChatButton, _filterActivityButton }
        };
    }

    /// <summary>
    /// Builds the right chat area: filter group | chat panel | separator | input area.
    /// </summary>
    private Grid BuildChatArea(StackPanel filterGroup)
    {
        var inputSeparator = new Border
        {
            Height = 1,
            Background = PaletteTokens.SeparatorBrush,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

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

        return chatArea;
    }

    /// <summary>
    /// Builds the main layout grid: sidebar | splitter | chat area, with splitter normalization.
    /// </summary>
    private static Grid BuildMainLayout(Grid sidebar, Grid chatArea)
    {
        var sidebarChatSplitter = new GridSplitter
        {
            Width = 4,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.SizeWestEast),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

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

        return mainGrid;
    }

    /// <summary>
    /// Wires a single filter toggle button: sets <see cref="TownhallViewModel.FilterMode"/>
    /// when checked and unchecks the other two buttons for mutual exclusivity.
    /// </summary>
    private IDisposable WireFilterButton(
        ToggleButton button,
        ToggleButton other1,
        ToggleButton other2,
        FilterMode mode)
    {
        return Observable.FromEventPattern<RoutedEventArgs>(
                h => button.IsCheckedChanged += h,
                h => button.IsCheckedChanged -= h)
            .Subscribe(_ =>
            {
                if (button.IsChecked != true) return;
                _viewModel!.FilterMode = mode;
                if (other1.IsChecked != false) other1.IsChecked = false;
                if (other2.IsChecked != false) other2.IsChecked = false;
            });
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

        // Wire filter toggle buttons to FilterMode using a shared helper that unchecks
        // the other two buttons when a button is checked, guarding against redundant
        // sets to avoid re-entrant event storms.
        _disposables.Add(WireFilterButton(_filterAllButton, _filterChatButton, _filterActivityButton, FilterMode.All));
        _disposables.Add(WireFilterButton(_filterChatButton, _filterAllButton, _filterActivityButton, FilterMode.ChatOnly));
        _disposables.Add(WireFilterButton(_filterActivityButton, _filterAllButton, _filterChatButton, FilterMode.ActivityOnly));

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
