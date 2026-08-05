using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Domain;
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
    private readonly TownhallNavigationPanel _navigationPanel;
    private readonly TownhallChatPanel _chatPanel;
    private readonly TownhallContextPolicySelector _contextPolicySelector;
    private readonly AgentBackendBindingPanel _backendBindingPanel;
    private readonly AgentTracePanel _tracePanel;
    private readonly AgentMemoryPanel _memoryPanel;
    private readonly AgentUsagePanel _usagePanel;
    private readonly TownhallInputArea _inputArea;
    private readonly ToggleButton _filterAllButton;
    private readonly ToggleButton _filterChatButton;
    private readonly ToggleButton _filterActivityButton;
    private readonly Button _traceButton;
    private readonly Button _memoryButton;
    private readonly Button _usageButton;
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

    /// <summary>Townhall Trace entry button (Phase 22.4 transparency surface).</summary>
    public Button TraceOpenButton => _traceButton;

    /// <summary>Townhall Memory entry button (Phase 22.4 transparency surface).</summary>
    public Button MemoryOpenButton => _memoryButton;

    /// <summary>Townhall Usage entry button (Phase 22.4 transparency surface).</summary>
    public Button UsageOpenButton => _usageButton;

    /// <summary>Hosted Trace panel for accessibility and integration proofs.</summary>
    internal AgentTracePanel TracePanel => _tracePanel;

    /// <summary>Hosted Memory panel for accessibility and integration proofs.</summary>
    internal AgentMemoryPanel MemoryPanel => _memoryPanel;

    /// <summary>Hosted Usage panel for accessibility and integration proofs.</summary>
    internal AgentUsagePanel UsagePanel => _usagePanel;

    public TownhallView()
    {
        _peoplePanel = new TownhallPeoplePanel { Background = PaletteTokens.SurfacePanelBrush };
        _navigationPanel = new TownhallNavigationPanel { Background = PaletteTokens.SurfacePanelBrush };
        _chatPanel = new TownhallChatPanel { Background = PaletteTokens.SurfacePanelBrush };
        _contextPolicySelector = new TownhallContextPolicySelector
        {
            Background = PaletteTokens.SurfacePanelBrush,
            IsVisible = false,
        };
        _backendBindingPanel = new AgentBackendBindingPanel
        {
            Background = PaletteTokens.SurfacePanelBrush,
            IsVisible = false,
        };
        _tracePanel = new AgentTracePanel
        {
            Background = PaletteTokens.SurfacePanelBrush,
        };
        _memoryPanel = new AgentMemoryPanel
        {
            Background = PaletteTokens.SurfacePanelBrush,
        };
        _usagePanel = new AgentUsagePanel
        {
            Background = PaletteTokens.SurfacePanelBrush,
        };
        _inputArea = new TownhallInputArea
        {
            Background = PaletteTokens.SurfacePanelBrush,
            VerticalAlignment = VerticalAlignment.Bottom
        };

        // Filter toggle buttons: All / Chat / Activity
        _filterAllButton = CreateMessageFilterToggle("All", "Show all messages", isChecked: true);
        _filterChatButton = CreateMessageFilterToggle("Chat", "Show chat messages only");
        _filterActivityButton = CreateMessageFilterToggle("Activity", "Show activity messages only");
        _traceButton = CreateTransparencyOpenerButton(
            "Trace",
            "Open or close agent trace evidence",
            "Opens or closes the agent trace evidence panel for the opened workspace.");
        _memoryButton = CreateTransparencyOpenerButton(
            "Memory",
            "Open or close agent durable memory",
            "Opens or closes the durable memory lifecycle panel for the opened workspace.");
        _usageButton = CreateTransparencyOpenerButton(
            "Usage",
            "Open or close agent usage and cost evidence",
            "Opens or closes the usage and cost evidence panel for the opened workspace.");

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
                _navigationPanel
            }
        };
        Grid.SetRow(_navigationPanel, 2);

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
    /// Builds the message-filter toggles and transparency panel openers as separate
    /// control groups so feed filtering and evidence inspection are distinct gestures.
    /// </summary>
    private StackPanel BuildFilterGroup()
    {
        var messageFilters = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingXs,
            Background = new SolidColorBrush(Color.FromArgb(0x22, 0x24, 0x33, 0x52)),
            Children =
            {
                _filterAllButton,
                _filterChatButton,
                _filterActivityButton,
            },
        };
        Avalonia.Automation.AutomationProperties.SetName(messageFilters, "Message filter");

        var groupSeparator = new Border
        {
            Width = 2,
            Height = 22,
            Margin = LayoutTokens.Symmetric(LayoutTokens.SpacingMd, 0),
            Background = new SolidColorBrush(Color.FromArgb(0xAA, 0x8B, 0x95, 0xA5)),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var evidenceOpeners = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingSm,
            Children =
            {
                _traceButton,
                _memoryButton,
                _usageButton,
            },
        };
        Avalonia.Automation.AutomationProperties.SetName(evidenceOpeners, "Transparency evidence panels");

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingMd,
            Margin = LayoutTokens.Inset(0, 0, 0, LayoutTokens.SpacingSm),
            Children =
            {
                messageFilters,
                groupSeparator,
                evidenceOpeners,
            },
        };
    }

    private static ToggleButton CreateMessageFilterToggle(
        string label,
        string automationName,
        bool isChecked = false)
    {
        var button = new ToggleButton
        {
            Content = TextStyles.Caption(label),
            IsChecked = isChecked,
            Focusable = true,
            IsTabStop = true,
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingSm, LayoutTokens.SpacingXxs),
            CornerRadius = LayoutTokens.RadiusFull,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };
        Avalonia.Automation.AutomationProperties.SetName(button, automationName);
        return button;
    }

    private static Button CreateTransparencyOpenerButton(
        string label,
        string automationName,
        string helpText)
    {
        var button = new Button
        {
            Content = TextStyles.Caption(label),
            Focusable = true,
            IsTabStop = true,
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingSm, LayoutTokens.SpacingXxs),
            CornerRadius = LayoutTokens.RadiusSm,
            Background = Brushes.Transparent,
            BorderBrush = PaletteTokens.TextSecondaryBrush,
            BorderThickness = new Thickness(1),
        };
        Avalonia.Automation.AutomationProperties.SetName(button, automationName);
        Avalonia.Automation.AutomationProperties.SetHelpText(button, helpText);
        return button;
    }

    /// <summary>
    /// Builds the right chat area: filter group | chat panel | transparency and workflow panels | input area.
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
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            Children =
            {
                filterGroup,
                _chatPanel,
                _tracePanel,
                _memoryPanel,
                _usagePanel,
                _backendBindingPanel,
                _contextPolicySelector,
                inputSeparator,
                _inputArea
            }
        };
        Grid.SetRow(filterGroup, 0);
        Grid.SetRow(_chatPanel, 1);
        Grid.SetRow(_tracePanel, 2);
        Grid.SetRow(_memoryPanel, 3);
        Grid.SetRow(_usagePanel, 4);
        Grid.SetRow(_backendBindingPanel, 5);
        Grid.SetRow(_contextPolicySelector, 6);
        Grid.SetRow(inputSeparator, 7);
        Grid.SetRow(_inputArea, 8);

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
        if (_viewModel is null || !_viewModel.IsInputEnabled)
        {
            return;
        }

        // Sync text from input to ViewModel draft, then send
        _viewModel.DraftText = _inputArea.InputText;
        _viewModel.SendMessageCommand.Execute().Subscribe();
    }

    private void WireViewModel()
    {
        _disposables?.Dispose();
        _disposables = new CompositeDisposable();

        if (_viewModel is null) return;

        _tracePanel.SetViewModel(_viewModel.TransparencyManagement);
        _memoryPanel.SetViewModel(_viewModel.TransparencyManagement);
        _usagePanel.SetViewModel(_viewModel.TransparencyManagement);
        _traceButton.Click += OnTraceButtonClick;
        _memoryButton.Click += OnMemoryButtonClick;
        _usageButton.Click += OnUsageButtonClick;
        _disposables.Add(Disposable.Create(() => _traceButton.Click -= OnTraceButtonClick));
        _disposables.Add(Disposable.Create(() => _memoryButton.Click -= OnMemoryButtonClick));
        _disposables.Add(Disposable.Create(() => _usageButton.Click -= OnUsageButtonClick));

        var transparencyManagement = _viewModel.TransparencyManagement;
        if (transparencyManagement is not null)
        {
            _disposables.Add(
                transparencyManagement.WhenAnyValue(x => x.IsTracePanelOpen)
                    .Subscribe(isOpen => ApplyTransparencyOpenerSelectedState(_traceButton, isOpen)));
            _disposables.Add(
                transparencyManagement.WhenAnyValue(x => x.IsMemoryPanelOpen)
                    .Subscribe(isOpen => ApplyTransparencyOpenerSelectedState(_memoryButton, isOpen)));
            _disposables.Add(
                transparencyManagement.WhenAnyValue(x => x.IsUsagePanelOpen)
                    .Subscribe(isOpen => ApplyTransparencyOpenerSelectedState(_usageButton, isOpen)));
        }

        // Populate people panel
        _peoplePanel.SetAgents(_viewModel.Agents);
        _peoplePanel.SetOnOpenDirectMessage(agentActorId =>
        {
            _viewModel.OpenDirectConversationCommand.Execute(agentActorId).Subscribe();
        });

        // Populate navigation panel
        _navigationPanel.SetOnChannelSelected(channelId =>
        {
            _viewModel.SelectChannelCommand.Execute(channelId).Subscribe();
        });
        _navigationPanel.SetOnDirectSelected(conversationId =>
        {
            _viewModel.SelectConversationCommand.Execute(conversationId).Subscribe();
        });
        _navigationPanel.SetChannels(_viewModel.Channels);
        _navigationPanel.SetDirectItems(_viewModel.DirectNavItems);

        _disposables.Add(
            Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                    h => _viewModel.DirectNavItems.CollectionChanged += h,
                    h => _viewModel.DirectNavItems.CollectionChanged -= h)
                .Subscribe(_ => _navigationPanel.SetDirectItems(_viewModel.DirectNavItems)));

        // Populate chat panel with initial messages (will be updated by FilteredMessages subscription below)
        if (_viewModel.Messages is not null)
        {
            _chatPanel.SetMessages(new ObservableCollection<TownhallMessage>(_viewModel.Messages));
        }

        _chatPanel.SetConversationHeader(_viewModel.ActiveConversationHeaderLabel);
        _inputArea.PlaceholderText = _viewModel.ActiveConversationInputPlaceholder;

        // React to active conversation changes: update navigation highlight and messages
        _disposables.Add(
            _viewModel.WhenAnyValue(x => x.ActiveConversationId)
                .Subscribe(_ =>
                {
                    _chatPanel.ResetForConversation();
                    _navigationPanel.SetChannels(_viewModel.Channels);
                    _navigationPanel.SetDirectItems(_viewModel.DirectNavItems);
                    SyncNavigationSelection();
                    _viewModel.PublishMemoryTownhallContext();
                }));

        _disposables.Add(
            _viewModel.WhenAnyValue(x => x.ActiveConversationHeaderLabel)
                .Subscribe(label => _chatPanel.SetConversationHeader(label)));

        _disposables.Add(
            _viewModel.WhenAnyValue(x => x.ActiveConversationInputPlaceholder)
                .Subscribe(placeholder => _inputArea.PlaceholderText = placeholder));

        // React to FilteredMessages changes (filter mode or underlying collection updates).
        _disposables.Add(
            _viewModel.FilteredMessages
                .Subscribe(filtered => _chatPanel.UpdateMessages(filtered)));

        _disposables.Add(
            _viewModel.WhenAnyValue(x => x.IsInputEnabled)
                .Subscribe(enabled => _inputArea.IsInputEnabled = enabled));

        _disposables.Add(
            _viewModel.WhenAnyValue(
                    x => x.IsContextPolicySelectorVisible,
                    x => x.ContextPolicySelectorIndex,
                    x => x.ContextPolicyStatusCaption,
                    x => x.IsContextPolicyOverrideActive,
                    x => x.IsInputEnabled)
                .Subscribe(tuple =>
                {
                    var (visible, selectorIndex, statusCaption, isOverrideActive, inputEnabled) = tuple;
                    _contextPolicySelector.IsSelectorVisible = visible;
                    _contextPolicySelector.SetPolicyProjection(selectorIndex, statusCaption, isOverrideActive);
                    _contextPolicySelector.SetSelectorEnabled(inputEnabled);
                }));

        void ApplyBackendBindingProjection()
        {
            if (_viewModel is null)
            {
                return;
            }

            _backendBindingPanel.IsPanelVisible = _viewModel.IsBackendBindingStatusVisible;
            _backendBindingPanel.SetWorkflowProjection(
                _viewModel.BackendBindingLabel,
                _viewModel.BackendAuthStatusCaption,
                _viewModel.IsBackendDisconnected,
                _viewModel.BackendCapabilityCaption,
                _viewModel.BackendSettingsCaption,
                string.IsNullOrEmpty(_viewModel.BackendMutationErrorCaption)
                    ? null
                    : _viewModel.BackendMutationErrorCaption,
                _viewModel.CanBindNativeHarness,
                _viewModel.CanUnbindBackend,
                _viewModel.CanEndSession,
                string.IsNullOrEmpty(_viewModel.AcpRuntimeCaption)
                    ? null
                    : _viewModel.AcpRuntimeCaption,
                _viewModel.CanProbeAcp,
                _viewModel.CanAuthenticateAcp,
                _viewModel.CanLogoutAcp,
                _viewModel.CanBindAcp,
                _viewModel.ShowAcpConfig);

            // Push draft fields into the panel when the VM is the source of truth
            // (bound ACP identity). User edits flow back via request handlers.
            if (!string.IsNullOrEmpty(_viewModel.AcpExecutableDraft))
            {
                _backendBindingPanel.AcpExecutablePath = _viewModel.AcpExecutableDraft;
            }

            if (!string.IsNullOrEmpty(_viewModel.AcpArgumentsDraft))
            {
                _backendBindingPanel.AcpArgumentsText = _viewModel.AcpArgumentsDraft;
            }

            if (!string.IsNullOrEmpty(_viewModel.AcpExpectedNameDraft))
            {
                _backendBindingPanel.AcpExpectedAgentName = _viewModel.AcpExpectedNameDraft;
            }

            if (!string.IsNullOrEmpty(_viewModel.AcpExpectedVersionDraft))
            {
                _backendBindingPanel.AcpExpectedAgentVersion = _viewModel.AcpExpectedVersionDraft;
            }
        }

        _disposables.Add(
            _viewModel.WhenAnyValue(
                    x => x.IsBackendBindingStatusVisible,
                    x => x.BackendBindingLabel,
                    x => x.BackendAuthStatusCaption,
                    x => x.IsBackendDisconnected)
                .Subscribe(_ => ApplyBackendBindingProjection()));
        _disposables.Add(
            _viewModel.WhenAnyValue(
                    x => x.BackendCapabilityCaption,
                    x => x.BackendSettingsCaption,
                    x => x.BackendMutationErrorCaption,
                    x => x.CanBindNativeHarness)
                .Subscribe(_ => ApplyBackendBindingProjection()));
        _disposables.Add(
            _viewModel.WhenAnyValue(
                    x => x.CanUnbindBackend,
                    x => x.CanEndSession,
                    x => x.AcpRuntimeCaption,
                    x => x.CanProbeAcp,
                    x => x.CanLogoutAcp)
                .Subscribe(_ => ApplyBackendBindingProjection()));
        _disposables.Add(
            _viewModel.WhenAnyValue(
                    x => x.CanAuthenticateAcp,
                    x => x.CanBindAcp,
                    x => x.ShowAcpConfig)
                .Subscribe(_ => ApplyBackendBindingProjection()));

        _backendBindingPanel.BindNativeHarnessRequested += (_, _) =>
        {
            _viewModel?.BindNativeHarnessCommand.Execute().Subscribe();
        };
        _backendBindingPanel.BindAcpRequested += (_, _) =>
        {
            if (_viewModel is null)
            {
                return;
            }

            _viewModel.AcpExecutableDraft = _backendBindingPanel.AcpExecutablePath;
            _viewModel.AcpArgumentsDraft = _backendBindingPanel.AcpArgumentsText;
            _viewModel.AcpExpectedNameDraft = _backendBindingPanel.AcpExpectedAgentName;
            _viewModel.AcpExpectedVersionDraft = _backendBindingPanel.AcpExpectedAgentVersion;
            _viewModel.BindAcpCommand.Execute().Subscribe();
        };
        _backendBindingPanel.UnbindRequested += (_, _) =>
        {
            _viewModel?.UnbindBackendCommand.Execute().Subscribe();
        };
        _backendBindingPanel.EndSessionRequested += (_, _) =>
        {
            _viewModel?.EndSessionCommand.Execute().Subscribe();
        };
        _backendBindingPanel.ProbeAcpRequested += (_, _) =>
        {
            _viewModel?.ProbeAcpCommand.Execute().Subscribe();
        };
        _backendBindingPanel.AuthenticateAcpRequested += (_, _) =>
        {
            _viewModel?.AuthenticateAcpCommand.Execute().Subscribe();
        };
        _backendBindingPanel.LogoutRequested += (_, _) =>
        {
            _viewModel?.LogoutAcpCommand.Execute().Subscribe();
        };

        _contextPolicySelector.PolicySelectionChanged += (_, index) =>
        {
            _viewModel?.SetContextPolicyFromSelectorCommand.Execute(index).Subscribe();
        };
        _contextPolicySelector.ClearOverrideRequested += (_, _) =>
        {
            _viewModel?.ClearContextPolicyOverrideCommand.Execute().Subscribe();
        };

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

    private void SyncNavigationSelection()
    {
        if (_viewModel is null)
        {
            return;
        }

        if (_viewModel.ActiveChannelId is { } channelId)
        {
            _navigationPanel.SyncChannelSelection(channelId);
            return;
        }

        if (_viewModel.ActiveConversationId is { } conversationId)
        {
            _navigationPanel.SyncDirectSelection(conversationId);
        }
    }

    public void Dispose()
    {
        _disposables?.Dispose();
        _disposables = null;
        _tracePanel.Dispose();
        _memoryPanel.Dispose();
        _usagePanel.Dispose();
    }

    private void OnTraceButtonClick(object? sender, RoutedEventArgs eventArgs) =>
        _viewModel?.TransparencyManagement?.ToggleTraceCommand.Execute().Subscribe();

    private void OnMemoryButtonClick(object? sender, RoutedEventArgs eventArgs) =>
        _viewModel?.TransparencyManagement?.ToggleMemoryCommand.Execute().Subscribe();

    private void OnUsageButtonClick(object? sender, RoutedEventArgs eventArgs) =>
        _viewModel?.TransparencyManagement?.ToggleUsageCommand.Execute().Subscribe();

    /// <summary>
    /// Paints a pressed/selected state on an evidence opener when its panel is
    /// open so the toolbar reflects open surfaces. Pure presentation; does not
    /// couple to the message-filter toggle group.
    /// </summary>
    private static void ApplyTransparencyOpenerSelectedState(Button button, bool isOpen)
    {
        var accent = PaletteTokens.PrimaryAccentColor;
        button.Background = isOpen
            ? new SolidColorBrush(Color.FromArgb(0x30, accent.R, accent.G, accent.B))
            : Brushes.Transparent;
        button.BorderBrush = isOpen
            ? PaletteTokens.PrimaryAccentBrush
            : PaletteTokens.TextSecondaryBrush;
    }
}
