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
        Width = 40;
        MinWidth = 40;
        MaxWidth = 40;

        var appLogo = new TextBlock
        {
            Text = "Z",
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            Foreground = (IBrush?)Application.Current!.Resources["TextActive"],
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        _explorerButton = BuildIconButton("≡", "Explorer", LeftPanelMode.Explorer);
        _sourceControlButton = BuildIconButton("⑂", "Source Control", LeftPanelMode.SourceControl);

        var topStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8,
            Children = { BuildLogoContainer(appLogo), _explorerButton, _sourceControlButton }
        };

        var settings = BuildIconButton("⚙", "Settings (deferred)", ActiveMode);

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto }
            },
            Margin = new Thickness(0),
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

    private static Border BuildLogoContainer(Control content) => new()
    {
        Height = 36,
        Margin = new Thickness(4, 8, 4, 4),
        Background = Brushes.Transparent,
        Child = content
    };

    private Border BuildIconButton(string glyph, string toolTip, LeftPanelMode modeForClick)
    {
        var text = new TextBlock
        {
            Text = glyph,
            FontSize = 13,
            Foreground = (IBrush?)Application.Current!.Resources["TextActive"],
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var border = new Border
        {
            Height = 30,
            Margin = new Thickness(4, 0, 4, 0),
            CornerRadius = new CornerRadius(6),
            Background = Brushes.Transparent,
            Child = text,
            Cursor = new Cursor(StandardCursorType.Hand)
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
        var inactiveBrush = Brushes.Transparent;

        _explorerButton.Background = ActiveMode == LeftPanelMode.Explorer ? activeBrush : inactiveBrush;
        _sourceControlButton.Background = ActiveMode == LeftPanelMode.SourceControl ? activeBrush : inactiveBrush;
    }
}
