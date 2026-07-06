using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
    private CompositeDisposable? _disposables;
    private ObservableCollection<TownhallMessage>? _currentMessages;

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

        var inputSeparator = new Border
        {
            Height = 1,
            Background = (IBrush?)Application.Current!.Resources["SeparatorBrush"],
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // Chat area: chat messages (fills) | separator | input area (auto)
        var chatArea = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            Children =
            {
                _chatPanel,
                inputSeparator,
                _inputArea
            }
        };
        Grid.SetRow(inputSeparator, 1);
        Grid.SetRow(_inputArea, 2);

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

        // Populate chat panel with initial messages
        SetChatMessages(_viewModel.Messages);

        // Update placeholder text based on active channel
        UpdatePlaceholder();

        // React to active channel changes: update channel list highlight and messages
        _disposables.Add(
            _viewModel.WhenAnyValue(x => x.ActiveChannelId)
                .Subscribe(_ =>
                {
                    _channelPanel.SetChannels(_viewModel.Channels);
                    SetChatMessages(_viewModel.Messages);
                    UpdatePlaceholder();
                }));

        // React to Messages reference changes (channel switch replaces the collection).
        _disposables.Add(
            _viewModel.WhenAnyValue(x => x.Messages)
                .Subscribe(messages =>
                {
                    SetChatMessages(messages);
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

    /// <summary>
    /// Sets the chat messages and subscribes to CollectionChanged for live appends.
    /// </summary>
    private void SetChatMessages(ObservableCollection<TownhallMessage>? messages)
    {
        // Unsubscribe from previous collection
        if (_currentMessages is not null)
        {
            _currentMessages.CollectionChanged -= OnCurrentMessagesChanged;
        }

        _currentMessages = messages;
        if (messages is not null)
        {
            _chatPanel.SetMessages(messages);
        }

        // Subscribe to CollectionChanged for live appends (e.g., SendMessageCommand)
        if (messages is not null)
        {
            messages.CollectionChanged += OnCurrentMessagesChanged;
        }
    }

    private void OnCurrentMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_currentMessages is not null)
        {
            _chatPanel.SetMessages(_currentMessages);
        }
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
        if (_currentMessages is not null)
        {
            _currentMessages.CollectionChanged -= OnCurrentMessagesChanged;
        }
        _disposables?.Dispose();
        _disposables = null;
    }
}
