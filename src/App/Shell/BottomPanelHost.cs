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

        var problemsTabButton = CreateModeButton(
            "Problems",
            LayoutTokens.Inset(LayoutTokens.SpacingXxs, LayoutTokens.SpacingXxs, 0, LayoutTokens.SpacingXxs),
            fontSizeSm: false,
            onClick: () => _viewModel?.SwitchToProblemsBottomCommand.Execute().Subscribe());

        var outputTabButton = CreateModeButton(
            "Output",
            margin: default,
            fontSizeSm: true,
            onClick: () => _viewModel?.SwitchToOutputBottomCommand.Execute().Subscribe());

        var testResultsTabButton = CreateModeButton(
            "Test Results",
            margin: default,
            fontSizeSm: true,
            onClick: () => _viewModel?.SwitchToTestResultsBottomCommand.Execute().Subscribe());

        var debugTabButton = CreateModeButton(
            "Debug",
            margin: default,
            fontSizeSm: true,
            onClick: () => _viewModel?.SwitchToDebugBottomCommand.Execute().Subscribe());

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
            Background = (IBrush?)Application.Current!.Resources["SurfacePanelBrush"],
            Padding = LayoutTokens.NoneThickness,
            // M5-allow: M1 introduced the 1px top seam above the bottom panel to preserve the raised-layer split.
            Margin = LayoutTokens.Inset(0, 1, 0, 0),
            Child = bottomContent,
            IsVisible = false,
        };
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
    }

    internal void ApplyBottomPanelVisibility(bool visible)
    {
        if (_splitterRow is null || _panelRow is null)
            throw new InvalidOperationException("Bottom panel host is not attached to a layout grid.");

        _splitterRow.Height = visible
            ? new GridLength(4, GridUnitType.Pixel)
            : new GridLength(0);
        _panelRow.Height = visible
            ? new GridLength(250)
            : new GridLength(0);
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
        var button = new Button
        {
            Content = label,
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingSm, LayoutTokens.SpacingXxs),
            Margin = margin,
            Background = Brushes.Transparent,
            BorderThickness = LayoutTokens.NoneThickness,
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
            Cursor = new Cursor(StandardCursorType.Hand),
        };

        if (fontSizeSm)
            button.FontSize = TypographyTokens.FontSizeSm;

        button.Click += (_, _) => onClick();
        return button;
    }
}
