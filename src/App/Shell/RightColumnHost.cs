using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Debugging.Presentation;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.Settings.Contracts;
using Zaide.UI.DesignSystem;

namespace Zaide.App.Shell;

/// <summary>
/// Shell-owned right column: editor tab bar, search bar, editor/welcome surface,
/// vertical splitter, and agent panel host.
/// </summary>
internal sealed class RightColumnHost
{
    public RightColumnHost(
        ISettingsService settings,
        EditorSearchViewModel searchViewModel,
        EditorLanguageInputViewModel languageInputViewModel,
        EditorBreakpointViewModel editorBreakpointViewModel,
        DebugCurrentLocationViewModel debugCurrentLocationViewModel)
    {
        EditorTabBar = new EditorTabBar();
        SearchBar = new SearchBar(searchViewModel);
        EditorView = new EditorView(
            settings,
            languageInputViewModel,
            editorBreakpointViewModel,
            debugCurrentLocationViewModel);
        WelcomeText = TextStyles.Body("Open a file to begin");
        WelcomeText.VerticalAlignment = VerticalAlignment.Center;
        WelcomeText.HorizontalAlignment = HorizontalAlignment.Center;
        WelcomeText.IsVisible = true;

        var verticalSplitter = new GridSplitter
        {
            Height = 4,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.SizeNorthSouth),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeDirection = GridResizeDirection.Rows,
            IsVisible = true,
        };

        var editorPanel = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
            },
            Background = (IBrush?)Application.Current!.Resources["SurfacePanelBrush"],
            // M5-allow: M1 introduced the 1px panel seam as a visual divider, not semantic spacing.
            Margin = LayoutTokens.Inset(1, 0, 0, 0),
            Children =
            {
                EditorTabBar,
                SearchBar,
                EditorView,
                WelcomeText,
            },
        };
        Grid.SetRow(EditorTabBar, 0);
        Grid.SetRow(SearchBar, 1);
        Grid.SetRow(EditorView, 2);
        Grid.SetRow(WelcomeText, 2);

        AgentPanelHostView = new AgentPanelHostView();

        Root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(2, GridUnitType.Star) },
                new RowDefinition { Height = new GridLength(4, GridUnitType.Pixel) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
            },
            Children = { editorPanel, verticalSplitter, AgentPanelHostView },
        };
        Grid.SetRow(editorPanel, 0);
        Grid.SetRow(verticalSplitter, 1);
        Grid.SetRow(AgentPanelHostView, 2);
    }

    public Grid Root { get; }

    public EditorTabBar EditorTabBar { get; }

    public SearchBar SearchBar { get; }

    public EditorView EditorView { get; }

    public TextBlock WelcomeText { get; }

    public AgentPanelHostView AgentPanelHostView { get; }

    public void AttachToLayoutGrid(Grid layoutRoot, int column = 5, int row = 0)
    {
        Grid.SetColumn(Root, column);
        Grid.SetRow(Root, row);
        layoutRoot.Children.Add(Root);
    }
}
