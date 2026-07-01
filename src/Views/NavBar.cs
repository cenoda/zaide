using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Zaide.Views;

public enum LeftPanelMode
{
    Explorer,
    SourceControl
}

public class NavBar : UserControl
{
    private readonly Border _explorerButton;
    private readonly Border _sourceControlButton;
    private readonly TextBlock _explorerIcon;
    private readonly TextBlock _sourceControlIcon;

    public static readonly StyledProperty<LeftPanelMode> ActiveModeProperty =
        AvaloniaProperty.Register<NavBar, LeftPanelMode>(nameof(ActiveMode), LeftPanelMode.Explorer);

    public LeftPanelMode ActiveMode
    {
        get => GetValue(ActiveModeProperty);
        set => SetValue(ActiveModeProperty, value);
    }

    public event Action<LeftPanelMode>? ModeChanged;

    public NavBar()
    {
        Width = 44;
        MinWidth = 44;
        MaxWidth = 44;

        // Logo — rounded circle with "Z" letter (matches concept's "A" logo)
        var logoText = new TextBlock
        {
            Text = "Z",
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            Foreground = (IBrush?)Application.Current!.Resources["TextActive"],
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var logoContainer = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(8),
            Background = (IBrush?)Application.Current!.Resources["PrimaryAccent"],
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 12),
            Child = logoText
        };

        _explorerIcon = new TextBlock
        {
            Text = "≡",
            FontSize = 16,
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondary"],
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        _sourceControlIcon = new TextBlock
        {
            Text = "⑂",
            FontSize = 16,
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondary"],
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        _explorerButton = BuildIconButton(_explorerIcon, LeftPanelMode.Explorer);
        _sourceControlButton = BuildIconButton(_sourceControlIcon, LeftPanelMode.SourceControl);

        var topStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { logoContainer, _explorerButton, _sourceControlButton }
        };

        // Settings icon at bottom
        var settingsIcon = new TextBlock
        {
            Text = "⚙",
            FontSize = 14,
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondary"],
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var settings = BuildIconButton(settingsIcon, ActiveMode);

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto }
            },
            Background = (IBrush?)Application.Current!.Resources["DeepBase"]
        };

        Grid.SetRow(topStack, 0);
        Grid.SetRow(settings, 1);

        root.Children.Add(topStack);
        root.Children.Add(settings);

        Content = root;

        this.GetObservable(ActiveModeProperty).Subscribe(_ => RefreshVisualState());
        RefreshVisualState();
    }

    private Border BuildIconButton(TextBlock icon, LeftPanelMode modeForClick)
    {
        var border = new Border
        {
            Width = 36,
            Height = 32,
            Margin = new Thickness(4, 2),
            CornerRadius = new CornerRadius(6),
            Background = Brushes.Transparent,
            Child = icon,
            Cursor = new Cursor(StandardCursorType.Hand),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        border.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
                return;

            if (modeForClick == LeftPanelMode.Explorer || modeForClick == LeftPanelMode.SourceControl)
            {
                if (ActiveMode != modeForClick)
                {
                    ActiveMode = modeForClick;
                    ModeChanged?.Invoke(modeForClick);
                }
            }

            e.Handled = true;
        };

        return border;
    }

    private void RefreshVisualState()
    {
        var activeBrush = (IBrush?)Application.Current!.Resources["PrimaryAccent"];
        var activeForeground = (IBrush?)Application.Current!.Resources["TextActive"];
        var inactiveForeground = (IBrush?)Application.Current!.Resources["TextSecondary"];

        _explorerButton.Background = ActiveMode == LeftPanelMode.Explorer ? activeBrush : Brushes.Transparent;
        _explorerIcon.Foreground = ActiveMode == LeftPanelMode.Explorer ? activeForeground : inactiveForeground;

        _sourceControlButton.Background = ActiveMode == LeftPanelMode.SourceControl ? activeBrush : Brushes.Transparent;
        _sourceControlIcon.Foreground = ActiveMode == LeftPanelMode.SourceControl ? activeForeground : inactiveForeground;
    }
}