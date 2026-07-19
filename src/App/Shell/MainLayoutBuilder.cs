using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Zaide.Features.Debugging.Presentation;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.SourceControl.Presentation;
using Zaide.Features.Townhall.Presentation;
using Zaide.Features.Workspace.Presentation;

namespace Zaide.App.Shell;

/// <summary>
/// Shell-owned main layout grid: column/row definitions, nav/left/townhall placement,
/// horizontal splitters, status bar, and attachment of bottom/right column hosts.
/// </summary>
internal sealed class MainLayoutBuilder
{
    public MainLayoutBuildResult Build(
        ISettingsService settings,
        EditorSearchViewModel searchViewModel,
        EditorLanguageInputViewModel languageInputViewModel,
        EditorBreakpointViewModel editorBreakpointViewModel,
        DebugCurrentLocationViewModel debugCurrentLocationViewModel)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                // 0: Nav bar (fixed 40px)
                new ColumnDefinition { Width = new GridLength(40) },
                // 1: Left panel (fixed 260px, min 180px)
                new ColumnDefinition { Width = new GridLength(260), MinWidth = 180, MaxWidth = 320 },
                // 2: Splitter between left panel and townhall
                new ColumnDefinition { Width = new GridLength(4, GridUnitType.Pixel) },
                // 3: Center — Townhall (star, dominant)
                new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star), MinWidth = 300 },
                // 4: Splitter between townhall and editor
                new ColumnDefinition { Width = new GridLength(4, GridUnitType.Pixel) },
                // 5: Right — Editor (star, smaller than center)
                new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star), MinWidth = 240 }
            },
            RowDefinitions =
            {
                // 0: Content area
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                // 1: Bottom panel splitter
                new RowDefinition { Height = new GridLength(0) },
                // 2: Bottom panel
                new RowDefinition { Height = new GridLength(0) },
                // 3: Status bar (24px, always visible)
                new RowDefinition { Height = new GridLength(24, GridUnitType.Pixel) }
            },
            Background = (IBrush?)Application.Current!.Resources["SurfaceBaseBrush"]
        };

        var bottomSplitterRow = grid.RowDefinitions[1];
        var bottomPanelRow = grid.RowDefinitions[2];
        var statusBarRow = grid.RowDefinitions[3];

        // --- Column 0: Nav Bar ---
        var navBar = new NavBar();
        Grid.SetColumn(navBar, 0);
        Grid.SetRow(navBar, 0);
        Grid.SetRowSpan(navBar, 4); // Full height
        grid.Children.Add(navBar);

        // --- Column 1: Left Panel (Explorer / Source Control) ---
        var fileTreeView = new FileTreeView();
        fileTreeView.RenderTransform = new TranslateTransform();
        Grid.SetColumn(fileTreeView, 1);
        Grid.SetRow(fileTreeView, 0);
        Grid.SetRowSpan(fileTreeView, 3);
        grid.Children.Add(fileTreeView);

        var sourceControlPanel = new SourceControlPanel();
        sourceControlPanel.IsVisible = false; // Hidden by default (Explorer mode)
        sourceControlPanel.RenderTransform = new TranslateTransform();
        Grid.SetColumn(sourceControlPanel, 1);
        Grid.SetRow(sourceControlPanel, 0);
        Grid.SetRowSpan(sourceControlPanel, 3);
        grid.Children.Add(sourceControlPanel);

        // --- Column 2: Splitter (left panel ↔ townhall) ---
        var leftSplitter = new GridSplitter
        {
            Width = 4,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.SizeWestEast),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        leftSplitter.DragCompleted += (_, _) =>
            GridLayoutResizeHelper.PreservePixelColumnAndNormalizeStarColumns(grid, 1, 3, 5);
        Grid.SetColumn(leftSplitter, 2);
        Grid.SetRow(leftSplitter, 0);
        Grid.SetRowSpan(leftSplitter, 3);
        grid.Children.Add(leftSplitter);

        // --- Column 3: Center — Townhall (real M3 view) ---
        var townhallView = new TownhallView();
        Grid.SetColumn(townhallView, 3);
        Grid.SetRow(townhallView, 0);
        grid.Children.Add(townhallView);

        // --- Column 4: Splitter (townhall ↔ editor) ---
        var rightSplitter = new GridSplitter
        {
            Width = 4,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.SizeWestEast),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        rightSplitter.DragCompleted += (_, _) =>
            GridLayoutResizeHelper.NormalizeStarColumns(grid, 3, 5);
        Grid.SetColumn(rightSplitter, 4);
        Grid.SetRow(rightSplitter, 0);
        grid.Children.Add(rightSplitter);

        // --- Column 5: Right — Editor (top) + Agent Panel (bottom) ---
        var rightColumnHost = new RightColumnHost(
            settings,
            searchViewModel,
            languageInputViewModel,
            editorBreakpointViewModel,
            debugCurrentLocationViewModel);
        rightColumnHost.AttachToLayoutGrid(grid);

        var bottomPanelHost = new BottomPanelHost(settings);
        bottomPanelHost.AttachToLayoutGrid(grid, bottomSplitterRow, bottomPanelRow);

        // --- Status Bar (full width, row 3) ---
        var statusBar = new StatusBar();
        Grid.SetColumn(statusBar, 0);
        Grid.SetColumnSpan(statusBar, 6); // Full width
        Grid.SetRow(statusBar, 3);
        grid.Children.Add(statusBar);

        return new MainLayoutBuildResult(
            grid,
            navBar,
            fileTreeView,
            sourceControlPanel,
            townhallView,
            statusBar,
            bottomPanelHost,
            rightColumnHost,
            bottomSplitterRow,
            bottomPanelRow,
            statusBarRow);
    }

    internal sealed record MainLayoutBuildResult(
        Grid Root,
        NavBar NavBar,
        FileTreeView FileTreeView,
        SourceControlPanel SourceControlPanel,
        TownhallView TownhallView,
        StatusBar StatusBar,
        BottomPanelHost BottomPanelHost,
        RightColumnHost RightColumnHost,
        RowDefinition BottomSplitterRow,
        RowDefinition BottomPanelRow,
        RowDefinition StatusBarRow);
}
