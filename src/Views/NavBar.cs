using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Zaide.Styles;
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
    private readonly Panel _explorerVisual;
    private readonly Panel _sourceControlVisual;
    private readonly Border _explorerActiveIndicator;
    private readonly Border _sourceControlActiveIndicator;
    private readonly Path _explorerIcon;
    private readonly Path _sourceControlIcon;
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
            CornerRadius = LayoutTokens.RadiusSm,
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
            CornerRadius = LayoutTokens.RadiusSm,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            IsVisible = false
        };

        // Nav icons use small local geometry so the 32x32 hit target remains the only clickable layer.
        _explorerIcon = CreateNavIcon(
            "M2,5 L6,5 L7.5,6.5 L14,6.5 L14,13 L2,13 Z",
            (IBrush?)Application.Current!.Resources["PrimaryAccentBrush"]);

        _sourceControlIcon = CreateNavIcon(
            "M5,3 A2,2 0 1 1 5,7 A2,2 0 1 1 5,3 M5,7 L5,10 A3,3 0 0 0 8,13 L11,13 M11,10 A2,2 0 1 1 11,14 A2,2 0 1 1 11,10",
            (IBrush?)Application.Current!.Resources["TextSecondaryBrush"]);

        // Hover overlay: 7% white, fills the icon button area
        _explorerHoverOverlay = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF)),
            CornerRadius = LayoutTokens.RadiusMd,
            IsVisible = false,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        _sourceControlHoverOverlay = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF)),
            CornerRadius = LayoutTokens.RadiusMd,
            IsVisible = false,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        // Explorer icon button
        _explorerButton = CreateIconButton("Explorer");
        _explorerVisual = new Panel
        {
            Children =
            {
                _explorerActiveIndicator,
                _explorerIcon,
                _explorerHoverOverlay
            }
        };
        _explorerVisual.RenderTransform = new TranslateTransform();
        _explorerButton.Child = _explorerVisual;

        // Source Control icon button
        _sourceControlButton = CreateIconButton("Source Control");
        _sourceControlVisual = new Panel
        {
            Children =
            {
                _sourceControlActiveIndicator,
                _sourceControlIcon,
                _sourceControlHoverOverlay
            }
        };
        _sourceControlVisual.RenderTransform = new TranslateTransform();
        _sourceControlButton.Child = _sourceControlVisual;

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
            Spacing = LayoutTokens.SpacingXs,
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
                    _explorerIcon.Stroke = (IBrush?)Application.Current!.Resources[
                        isExplorer ? "PrimaryAccentBrush" : "TextSecondaryBrush"];
                    _sourceControlIcon.Stroke = (IBrush?)Application.Current!.Resources[
                        !isExplorer ? "PrimaryAccentBrush" : "TextSecondaryBrush"];

                    _ = AnimateModeSwitchAsync(isExplorer);
                }));
    }

    private async Task AnimateModeSwitchAsync(bool isExplorer)
    {
        if (_explorerVisual.RenderTransform is not TranslateTransform)
        {
            _explorerVisual.RenderTransform = new TranslateTransform();
        }

        if (_sourceControlVisual.RenderTransform is not TranslateTransform)
        {
            _sourceControlVisual.RenderTransform = new TranslateTransform();
        }

        if (isExplorer)
        {
            await Task.WhenAll(
                Animations.RunAsync(_explorerVisual, Animations.NavEnter(HorizontalDirection.Left)),
                Animations.RunAsync(_sourceControlVisual, Animations.NavExit(HorizontalDirection.Right)));
        }
        else
        {
            await Task.WhenAll(
                Animations.RunAsync(_explorerVisual, Animations.NavExit(HorizontalDirection.Left)),
                Animations.RunAsync(_sourceControlVisual, Animations.NavEnter(HorizontalDirection.Right)));
        }
    }

    private static Path CreateNavIcon(string data, IBrush? stroke)
    {
        return new Path
        {
            Data = StreamGeometry.Parse(data),
            Width = 16,
            Height = 16,
            Stroke = stroke,
            StrokeThickness = 1.8,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            Stretch = Stretch.Uniform,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static Border CreateIconButton(string tooltip)
    {
        var button = new Border
        {
            Width = 32,
            Height = 32,
            Background = Brushes.Transparent,
            CornerRadius = LayoutTokens.RadiusMd,
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
