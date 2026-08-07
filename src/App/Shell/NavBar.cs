using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Zaide.UI.DesignSystem;

namespace Zaide.App.Shell;
/// <summary>
/// Far-left icon-only vertical nav bar (~40px) for switching between
/// Explorer and Source Control left-panel modes.
/// Active icon uses PrimaryAccentBrush; inactive uses TextSecondaryBrush.
/// Hover uses <see cref="AppButton.IconSurface"/> interactive theme.
/// </summary>
public class NavBar : Panel, IDisposable
{
    private readonly Border _explorerButton;
    private readonly Border _sourceControlButton;
    private readonly Panel _explorerVisual;
    private readonly Panel _sourceControlVisual;
    private readonly Border _explorerActiveIndicator;
    private readonly Border _sourceControlActiveIndicator;
    private readonly Control _explorerIcon;
    private readonly Control _sourceControlIcon;
    private CompositeDisposable? _disposables;
    private bool _isExplorerActive = true;

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
        ThemeBinding.SetBrush(this, BackgroundProperty, "SurfaceBaseBrush");

        // Active indicator: 3px-wide vertical bar on the left edge, 20px tall
        _explorerActiveIndicator = new Border
        {
            Width = 3,
            Height = 20,
            CornerRadius = LayoutTokens.RadiusSm,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            IsVisible = true // Explorer is active by default
        };
        ThemeBinding.SetBrush(_explorerActiveIndicator, Border.BackgroundProperty, "PrimaryAccentBrush");

        _sourceControlActiveIndicator = new Border
        {
            Width = 3,
            Height = 20,
            CornerRadius = LayoutTokens.RadiusSm,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            IsVisible = false
        };
        ThemeBinding.SetBrush(_sourceControlActiveIndicator, Border.BackgroundProperty, "PrimaryAccentBrush");

        _explorerIcon = IconFactory.Create("Icon.Explorer", "PrimaryAccentBrush", 16);
        _sourceControlIcon = IconFactory.Create("Icon.SourceControl", "TextSecondaryBrush", 16);

        _explorerVisual = new Panel
        {
            Children =
            {
                _explorerActiveIndicator,
                _explorerIcon
            }
        };
        _explorerVisual.RenderTransform = new TranslateTransform();
        _explorerButton = AppButton.IconSurface(_explorerVisual, tooltip: "Explorer");

        _sourceControlVisual = new Panel
        {
            Children =
            {
                _sourceControlActiveIndicator,
                _sourceControlIcon
            }
        };
        _sourceControlVisual.RenderTransform = new TranslateTransform();
        _sourceControlButton = AppButton.IconSurface(_sourceControlVisual, tooltip: "Source Control");

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
        var separator = new Border
        {
            Width = 1,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        ThemeBinding.SetBrush(separator, Border.BackgroundProperty, "SeparatorBrush");
        Children.Add(separator);

        ThemeBinding.SubscribeVariantChanged(this, ApplyIconForegrounds);

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
                    _isExplorerActive = isExplorer;

                    // Active indicator visibility
                    _explorerActiveIndicator.IsVisible = isExplorer;
                    _sourceControlActiveIndicator.IsVisible = !isExplorer;

                    ApplyIconForegrounds();

                    _ = AnimateModeSwitchAsync(isExplorer);
                }));
    }

    private void ApplyIconForegrounds()
    {
        IconFactory.SetForeground(
            _explorerIcon,
            ThemeBinding.GetBrush(_isExplorerActive ? "PrimaryAccentBrush" : "TextSecondaryBrush"));
        IconFactory.SetForeground(
            _sourceControlIcon,
            ThemeBinding.GetBrush(_isExplorerActive ? "TextSecondaryBrush" : "PrimaryAccentBrush"));
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

    public void Dispose()
    {
        _disposables?.Dispose();
        _disposables = null;
    }
}
