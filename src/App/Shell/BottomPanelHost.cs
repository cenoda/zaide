using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Zaide.Features.Debugging.Presentation;
using Zaide.Features.ProjectSystem.Presentation;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Terminal.Presentation;
using Zaide.UI.DesignSystem;

namespace Zaide.App.Shell;

/// <summary>
/// Shell-owned bottom panel: mode strip, multi-surface content host, splitter,
/// and row-height wiring for Terminal / Problems / Output / Test Results / Debug.
/// </summary>
internal sealed class BottomPanelHost
{
    private RowDefinition? _splitterRow;
    private RowDefinition? _panelRow;
    private MainWindowViewModel? _viewModel;

    // Mode strip buttons for visual state updates
    private Button? _terminalButton;
    private Button? _problemsButton;
    private Button? _outputButton;
    private Button? _testResultsButton;
    private Button? _debugButton;

    public BottomPanelHost(ISettingsService settings)
    {
        TerminalTabHost = new TerminalTabHost(settings);
        ProblemsPanel = new ProblemsPanel { IsVisible = false };
        OutputPanel = new OutputPanel { IsVisible = false };
        TestResultsPanel = new TestResultsPanel { IsVisible = false };
        DebugPanel = new DebugPanel { IsVisible = false };

        Splitter = new GridSplitter
        {
            Height = 4,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.SizeNorthSouth),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeDirection = GridResizeDirection.Rows,
            IsVisible = false,
        };

        var terminalTabButton = CreateModeButton(
            "Terminal",
            LayoutTokens.Inset(LayoutTokens.SpacingSm, LayoutTokens.SpacingXxs, 0, LayoutTokens.SpacingXxs),
            fontSizeSm: false,
            onClick: () => _viewModel?.SwitchToTerminalBottomCommand.Execute().Subscribe());
        _terminalButton = terminalTabButton;

        var problemsTabButton = CreateModeButton(
            "Problems",
            LayoutTokens.Inset(LayoutTokens.SpacingXxs, LayoutTokens.SpacingXxs, 0, LayoutTokens.SpacingXxs),
            fontSizeSm: false,
            onClick: () => _viewModel?.SwitchToProblemsBottomCommand.Execute().Subscribe());
        _problemsButton = problemsTabButton;

        var outputTabButton = CreateModeButton(
            "Output",
            margin: default,
            fontSizeSm: true,
            onClick: () => _viewModel?.SwitchToOutputBottomCommand.Execute().Subscribe());
        _outputButton = outputTabButton;

        var testResultsTabButton = CreateModeButton(
            "Test Results",
            margin: default,
            fontSizeSm: true,
            onClick: () => _viewModel?.SwitchToTestResultsBottomCommand.Execute().Subscribe());
        _testResultsButton = testResultsTabButton;

        var debugTabButton = CreateModeButton(
            "Debug",
            margin: default,
            fontSizeSm: true,
            onClick: () => _viewModel?.SwitchToDebugBottomCommand.Execute().Subscribe());
        _debugButton = debugTabButton;

        var bottomModeStrip = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingXxs,
            Children =
            {
                terminalTabButton,
                problemsTabButton,
                outputTabButton,
                testResultsTabButton,
                debugTabButton,
            },
        };

        var bottomContent = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
            },
            Children =
            {
                bottomModeStrip,
                TerminalTabHost,
                ProblemsPanel,
                OutputPanel,
                TestResultsPanel,
                DebugPanel,
            },
        };
        Grid.SetRow(bottomModeStrip, 0);
        Grid.SetRow(TerminalTabHost, 1);
        Grid.SetRow(ProblemsPanel, 1);
        Grid.SetRow(OutputPanel, 1);
        Grid.SetRow(TestResultsPanel, 1);
        Grid.SetRow(DebugPanel, 1);

        PanelBorder = new Border
        {
            Padding = LayoutTokens.NoneThickness,
            // M5-allow: M1 introduced the 1px top seam above the bottom panel to preserve the raised-layer split.
            Margin = LayoutTokens.Inset(0, 1, 0, 0),
            Child = bottomContent,
            IsVisible = false,
        };
        ThemeBinding.SetBrush(PanelBorder, Border.BackgroundProperty, "SurfacePanelBrush");
    }

    public TerminalTabHost TerminalTabHost { get; }

    public ProblemsPanel ProblemsPanel { get; }

    public OutputPanel OutputPanel { get; }

    public TestResultsPanel TestResultsPanel { get; }

    public DebugPanel DebugPanel { get; }

    public Border PanelBorder { get; }

    public GridSplitter Splitter { get; }

    public void AttachToLayoutGrid(
        Grid layoutRoot,
        RowDefinition splitterRow,
        RowDefinition panelRow,
        int contentColumnStart = 3,
        int contentColumnSpan = 3,
        int splitterRowIndex = 1,
        int panelRowIndex = 2)
    {
        _splitterRow = splitterRow;
        _panelRow = panelRow;

        Grid.SetColumn(Splitter, contentColumnStart);
        Grid.SetColumnSpan(Splitter, contentColumnSpan);
        Grid.SetRow(Splitter, splitterRowIndex);
        layoutRoot.Children.Add(Splitter);

        Grid.SetColumn(PanelBorder, contentColumnStart);
        Grid.SetColumnSpan(PanelBorder, contentColumnSpan);
        Grid.SetRow(PanelBorder, panelRowIndex);
        layoutRoot.Children.Add(PanelBorder);
    }

    public void WireToViewModel(MainWindowViewModel viewModel, CompositeDisposable disposables)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        disposables.Add(viewModel.WhenAnyValue(x => x.IsBottomPanelVisible)
            .Subscribe(ApplyBottomPanelVisibility));

        disposables.Add(viewModel.WhenAnyValue(x => x.BottomPanelMode)
            .Subscribe(ApplyBottomPanelMode));

        // F7: Wire mode strip button visual states
        disposables.Add(viewModel.WhenAnyValue(x => x.IsTerminalBottomMode)
            .Subscribe(active => UpdateModeButtonStyle(_terminalButton!, active)));

        disposables.Add(viewModel.WhenAnyValue(x => x.IsProblemsBottomMode)
            .Subscribe(active => UpdateModeButtonStyle(_problemsButton!, active)));

        disposables.Add(viewModel.WhenAnyValue(x => x.IsOutputBottomMode)
            .Subscribe(active => UpdateModeButtonStyle(_outputButton!, active)));

        disposables.Add(viewModel.WhenAnyValue(x => x.IsTestResultsBottomMode)
            .Subscribe(active => UpdateModeButtonStyle(_testResultsButton!, active)));

        disposables.Add(viewModel.WhenAnyValue(x => x.IsDebugBottomMode)
            .Subscribe(active => UpdateModeButtonStyle(_debugButton!, active)));
    }

    private static void UpdateModeButtonStyle(Button button, bool isActive)
    {
        if (button is null)
            return;

        var activeBrush = ThemeBinding.GetBrush("TextPrimaryBrush");
        var inactiveBrush = ThemeBinding.GetBrush("TextSecondaryBrush");
        var accentBrush = ThemeBinding.GetBrush("AccentBrush");

        button.Foreground = isActive ? activeBrush : inactiveBrush;
        button.BorderBrush = isActive ? accentBrush : Brushes.Transparent;
        button.BorderThickness = isActive ? new Thickness(0, 0, 0, 2) : new Thickness(0);
    }

    internal void ApplyBottomPanelVisibility(bool visible)
    {
        if (_splitterRow is null || _panelRow is null)
            throw new InvalidOperationException("Bottom panel host is not attached to a layout grid.");

        _splitterRow.Height = visible
            ? new GridLength(4, GridUnitType.Pixel)
            : new GridLength(0);
        // Default open height 250px. Content row MinHeight (MainLayoutBuilder) clamps
        // GridSplitter drag so this panel cannot consume the Townhall/editor band.
        _panelRow.Height = visible
            ? new GridLength(250)
            : new GridLength(0);
        _panelRow.MinHeight = visible ? 80 : 0;
        Splitter.IsVisible = visible;
        PanelBorder.IsVisible = visible;

        if (visible && _viewModel?.BottomPanelMode == BottomPanelMode.Terminal)
            FocusAndStartActiveTerminalSession();
    }

    internal void ApplyBottomPanelMode(BottomPanelMode mode)
    {
        TerminalTabHost.IsVisible = mode == BottomPanelMode.Terminal;
        ProblemsPanel.IsVisible = mode == BottomPanelMode.Problems;
        OutputPanel.IsVisible = mode == BottomPanelMode.Output;
        TestResultsPanel.IsVisible = mode == BottomPanelMode.TestResults;
        DebugPanel.IsVisible = mode == BottomPanelMode.Debug;

        if (mode == BottomPanelMode.Terminal && _viewModel is { IsBottomPanelVisible: true })
            FocusAndStartActiveTerminalSession();
    }

    private void FocusAndStartActiveTerminalSession()
    {
        TerminalTabHost.FocusActiveSession();
        if (_viewModel is not null)
            _ = _viewModel.TerminalHost.EnsureActiveSessionStartedAsync();
    }

    private static Button CreateModeButton(
        string label,
        Thickness margin,
        bool fontSizeSm,
        Action onClick)
    {
        var button = AppButton.ToolbarLabel(label, margin, fontSizeSm);
        button.Click += (_, _) => onClick();
        return button;
    }
}
