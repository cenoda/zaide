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
/// </summary>
public class NavBar : Panel, IDisposable
{
    private readonly Border _explorerButton;
    private readonly Border _sourceControlButton;
    private readonly Border _explorerActiveIndicator;
    private readonly Border _sourceControlActiveIndicator;
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
            IsVisible = false
        };

        // Explorer icon button
        _explorerButton = CreateIconButton("\uD83D\uDCC1", "Explorer");
        _explorerButton.Child = CreateIconContent("\uD83D\uDCC1", _explorerActiveIndicator);

        // Source Control icon button
        _sourceControlButton = CreateIconButton("\uD83D\uDD04", "Source Control");
        _sourceControlButton.Child = CreateIconContent("\uD83D\uDD04", _sourceControlActiveIndicator);

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

        // Update active indicators when mode changes
        _disposables.Add(
            _viewModel.WhenAnyValue(x => x.LeftPanelMode)
                .Subscribe(mode =>
                {
                    _explorerActiveIndicator.IsVisible = mode == LeftPanelMode.Explorer;
                    _sourceControlActiveIndicator.IsVisible = mode == LeftPanelMode.SourceControl;
                }));
    }

    private static Border CreateIconButton(string icon, string tooltip)
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

    private static Panel CreateIconContent(string icon, Border activeIndicator)
    {
        var iconText = new TextBlock
        {
            Text = icon,
            FontSize = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"]
        };

        var panel = new Panel
        {
            Children =
            {
                activeIndicator,
                iconText
            }
        };

        return panel;
    }

    public void Dispose()
    {
        _disposables?.Dispose();
        _disposables = null;
    }
}