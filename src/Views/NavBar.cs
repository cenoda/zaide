using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Zaide.ViewModels;

namespace Zaide.Views;

/// <summary>
/// Far-left icon-only vertical nav bar (~40px) for switching between
/// Explorer and Source Control left-panel modes.
/// Active icon uses PrimaryAccentBrush; inactive uses TextSecondaryBrush.
/// Hover shows a #12FFFFFF (7% white) overlay per M0.5 spec.
/// </summary>
public class NavBar : Panel, IDisposable
{
    private readonly Border _explorerButton;
    private readonly Border _sourceControlButton;
    private readonly Border _explorerActiveIndicator;
    private readonly Border _sourceControlActiveIndicator;
    private readonly Control _explorerIcon;
    private readonly Control _sourceControlIcon;
    private readonly Border _explorerHoverOverlay;
    private readonly Border _sourceControlHoverOverlay;
    private CompositeDisposable? _disposables;

    /// <summary>
    /// Binds to a MainWindowViewModel to drive mode switching.
    /// </summary>
    public MainWindowViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            _viewModel = value;
            WireViewModel();
        }
    }
    private MainWindowViewModel? _viewModel;

    public NavBar()
    {
        Width = 40;
        Background = (IBrush?)Application.Current!.Resources["SurfaceBaseBrush"];

        // Active indicator: 3px-wide vertical bar on the left edge, 20px tall
        _explorerActiveIndicator = new Border
        {
            Width = 3,
            Height = 20,
            Background = (IBrush?)Application.Current!.Resources["PrimaryAccentBrush"],
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            IsVisible = true // Explorer is active by default
        };

        _sourceControlActiveIndicator = new Border
        {
            Width = 3,
            Height = 20,
            Background = (IBrush?)Application.Current!.Resources["PrimaryAccentBrush"],
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            IsVisible = false
        };

        // Icon geometry — starts with Explorer active (PrimaryAccentBrush), SC inactive (TextSecondaryBrush)
        _explorerIcon = IconFactory.Create(
            "Icon.Folder",
            (IBrush?)Application.Current!.Resources["PrimaryAccentBrush"]);

        _sourceControlIcon = IconFactory.Create(
            "Icon.GitBranch",
            (IBrush?)Application.Current!.Resources["TextSecondaryBrush"]);

        // Hover overlay: 7% white, fills the icon button area
        _explorerHoverOverlay = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF)),
            CornerRadius = new CornerRadius(8),
            IsVisible = false,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        _sourceControlHoverOverlay = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF)),
            CornerRadius = new CornerRadius(8),
            IsVisible = false,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        // Explorer icon button
        _explorerButton = CreateIconButton("Explorer");
        _explorerButton.Child = new Panel
        {
            Children =
            {
                _explorerActiveIndicator,
                _explorerIcon,
                _explorerHoverOverlay
            }
        };

        // Source Control icon button
        _sourceControlButton = CreateIconButton("Source Control");
        _sourceControlButton.Child = new Panel
        {
            Children =
            {
                _sourceControlActiveIndicator,
                _sourceControlIcon,
                _sourceControlHoverOverlay
            }
        };

        // Hover enter/exit on explorer button
        _explorerButton.PointerEntered += (_, _) => { _explorerHoverOverlay.IsVisible = true; };
        _explorerButton.PointerExited += (_, _) => { _explorerHoverOverlay.IsVisible = false; };

        // Hover enter/exit on source control button
        _sourceControlButton.PointerEntered += (_, _) => { _sourceControlHoverOverlay.IsVisible = true; };
        _sourceControlButton.PointerExited += (_, _) => { _sourceControlHoverOverlay.IsVisible = false; };

        // Layout: vertical stack of icons centered in the 40px column
        var iconStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 4,
            Children =
            {
                _explorerButton,
                _sourceControlButton
            }
        };

        Children.Add(iconStack);

        // Separator on the right edge
        Children.Add(new Border
        {
            Width = 1,
            Background = (IBrush?)Application.Current!.Resources["SeparatorBrush"],
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Stretch
        });

        // Wire click handlers
        _explorerButton.PointerPressed += (_, _) =>
        {
            if (ViewModel is not null)
                ViewModel.SwitchToExplorerCommand.Execute().Subscribe();
        };

        _sourceControlButton.PointerPressed += (_, _) =>
        {
            if (ViewModel is not null)
                ViewModel.SwitchToSourceControlCommand.Execute().Subscribe();
        };
    }

    private void WireViewModel()
    {
        _disposables?.Dispose();
        _disposables = new CompositeDisposable();

        if (_viewModel is null) return;

        // Update active indicators and icon colors when mode changes
        _disposables.Add(
            _viewModel.WhenAnyValue(x => x.LeftPanelMode)
                .Subscribe(mode =>
                {
                    var isExplorer = mode == LeftPanelMode.Explorer;

                    // Active indicator visibility
                    _explorerActiveIndicator.IsVisible = isExplorer;
                    _sourceControlActiveIndicator.IsVisible = !isExplorer;

                    // Icon color: PrimaryAccentBrush when active, TextSecondaryBrush when inactive
                    IconFactory.SetForeground(
                        _explorerIcon,
                        (IBrush?)Application.Current!.Resources[
                            isExplorer ? "PrimaryAccentBrush" : "TextSecondaryBrush"]);
                    IconFactory.SetForeground(
                        _sourceControlIcon,
                        (IBrush?)Application.Current!.Resources[
                            !isExplorer ? "PrimaryAccentBrush" : "TextSecondaryBrush"]);
                }));
    }

    private static Border CreateIconButton(string tooltip)
    {
        var button = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(8),
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        ToolTip.SetTip(button, tooltip);
        return button;
    }

    public void Dispose()
    {
        _disposables?.Dispose();
        _disposables = null;
    }
}
