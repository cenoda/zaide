using System;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Zaide.UI.DesignSystem;

namespace Zaide.App.Shell;
/// <summary>
/// Status bar at the very bottom of the window.
/// Shows app name, cursor position, language, project, branch, and AI model.
/// Thin bar (~24px height), full width.
/// Only Settings is interactive; other segments are display-only status (no button affordance).
/// </summary>
public class StatusBar : ReactiveUserControl<StatusBarViewModel>
{
    internal static string? FormatConfiguredModel(string? model) =>
        string.IsNullOrWhiteSpace(model) ? null : $"configured: {model}";
    private readonly TextBlock _caretText = TextStyles.Caption("");
    private readonly TextBlock _languageText = TextStyles.Caption("—");
    private readonly TextBlock _languageIntelligenceText = TextStyles.Caption("");
    private readonly Control _languageIntelligenceSegment;
    private readonly TextBlock _projectText = TextStyles.Caption("Zaide");
    private readonly TextBlock _branchText = TextStyles.Caption("");
    private readonly TextBlock _documentText = TextStyles.Caption("—");
    private readonly TextBlock _statusMessageText = TextStyles.Caption("");
    private readonly TextBlock _modelText;
    private readonly Button _settingsButton;
    private readonly Control _settingsIcon;
    private readonly TextBlock _settingsAppNameText;
    private IBrush? _settingsActiveBackground;
    private IBrush SettingsActiveBackground => _settingsActiveBackground ??= ThemeBinding.GetBrush("OverlaySelectedBrush");
    private IBrush? _settingsHoverBackground;
    private IBrush SettingsHoverBackground => _settingsHoverBackground ??= ThemeBinding.GetBrush("OverlayHoverBrush");
    private IBrush? _settingsPressedBackground;
    private IBrush SettingsPressedBackground => _settingsPressedBackground ??= ThemeBinding.GetBrush("OverlayPressedBrush");
    private bool _isSettingsButtonActive;

    public StatusBar()
    {
        Height = 24;
        Background = (IBrush?)Application.Current!.Resources["SurfaceBaseBrush"];
        ActualThemeVariantChanged += (_, _) =>
        {
            _settingsActiveBackground = null;
            _settingsHoverBackground = null;
            _settingsPressedBackground = null;
        };

        _settingsIcon = IconFactory.Create(
            "Icon.Config",
            (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
            14);

        _settingsAppNameText = TextStyles.Brand("Zaide");
        _settingsAppNameText.VerticalAlignment = VerticalAlignment.Center;
        _settingsAppNameText.Margin = LayoutTokens.Inset(LayoutTokens.SpacingXs, 0, 0, 0);
        ApplySettingsButtonVisualState(false);

        // Configured model (far-right caption). Vertically centered with status segments.
        _modelText = TextStyles.Caption("");
        _modelText.HorizontalAlignment = HorizontalAlignment.Right;
        _modelText.VerticalAlignment = VerticalAlignment.Center;
        _modelText.Margin = LayoutTokens.Inset(0, 0, LayoutTokens.SpacingMd, 0);
        _modelText.Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"];

        // Settings is the only interactive segment (OpenSettingsCommand).
        _settingsButton = BuildSettingsButton(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _settingsIcon, _settingsAppNameText }
        });
        WireSettingsButtonPointerFeedback(_settingsButton);

        _languageIntelligenceText.Foreground =
            (IBrush?)Application.Current!.Resources["TextSecondaryBrush"];
        _languageIntelligenceSegment = BuildStatusSegment("Icon.Code", _languageIntelligenceText);
        _languageIntelligenceSegment.IsVisible = false;

        // Transient "Opened: …" / save/search feedback. Bare TextBlocks default to
        // Stretch and paint text at the top unless centered.
        _statusMessageText.Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"];
        _statusMessageText.VerticalAlignment = VerticalAlignment.Center;
        _statusMessageText.Margin = LayoutTokens.Inset(0, 0, LayoutTokens.SpacingMd, 0);
        _statusMessageText.MaxWidth = 320;
        _statusMessageText.TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;

        var leftStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = LayoutTokens.SpacingMd,
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingMd, 0, 0, 0),
            Children =
            {
                _settingsButton,
                BuildStatusSegment("Icon.Text", _documentText),
                BuildStatusSegment("Icon.Selection", _caretText),
                BuildStatusSegment("Icon.Code", _languageText),
                _languageIntelligenceSegment,
                BuildStatusSegment("Icon.Project", _projectText),
                BuildStatusSegment("Icon.GitBranch", _branchText),
                _statusMessageText
            }
        };

        // Grid: left stack auto-width, model text fills remaining
        var layout = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
            Children =
            {
                leftStack,
                _modelText
            }
        };
        Grid.SetColumn(leftStack, 0);
        Grid.SetColumn(_modelText, 1);

        Content = layout;

        // --- Reactive Bindings ---
        this.WhenActivated(d =>
        {
            if (ViewModel is null) return;
            _settingsButton.Command = ViewModel.OpenSettingsCommand;

            d.Add(ViewModel.WhenAnyValue(x => x.CaretText).Subscribe(Observer.Create<string>(text => _caretText.Text = text)));
            d.Add(ViewModel.WhenAnyValue(x => x.LanguageText).Subscribe(Observer.Create<string>(text => _languageText.Text = text)));
            d.Add(ViewModel.WhenAnyValue(x => x.LanguageIntelligenceText)
                .Subscribe(Observer.Create<string>(text =>
                {
                    _languageIntelligenceText.Text = text;
                    _languageIntelligenceSegment.IsVisible = !string.IsNullOrEmpty(text);
                })));
            d.Add(ViewModel.WhenAnyValue(x => x.ProjectText).Subscribe(Observer.Create<string>(text => _projectText.Text = text)));
            d.Add(ViewModel.WhenAnyValue(x => x.BranchText).Subscribe(Observer.Create<string>(text => _branchText.Text = text)));
            d.Add(ViewModel.WhenAnyValue(x => x.DocumentText).Subscribe(Observer.Create<string>(text => _documentText.Text = text)));
            d.Add(ViewModel.WhenAnyValue(x => x.StatusMessage)
                .Subscribe(Observer.Create<string?>(msg =>
                {
                    _statusMessageText.Text = msg ?? "";
                    _statusMessageText.IsVisible = msg is not null;
                })));
            d.Add(ViewModel.WhenAnyValue(x => x.ConfiguredModel)
                .Subscribe(Observer.Create<string?>(model =>
                {
                    _modelText.Text = FormatConfiguredModel(model) ?? "";
                    _modelText.IsVisible = model is not null;
                })));
            d.Add(ViewModel.WhenAnyValue(x => x.IsSettingsOpen)
                .Subscribe(Observer.Create<bool>(isOpen =>
                {
                    ApplySettingsButtonVisualState(isOpen);
                    UpdateSettingsButtonBackground();
                })));
        });
    }

    private void ApplySettingsButtonVisualState(bool isActive)
    {
        _isSettingsButtonActive = isActive;
        var accentBrush = (IBrush?)Application.Current!.Resources["PrimaryAccentBrush"];
        var secondaryBrush = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"];
        IconFactory.SetForeground(_settingsIcon, isActive ? accentBrush : secondaryBrush);
        _settingsAppNameText.Foreground = isActive ? accentBrush : secondaryBrush;
    }

    private void UpdateSettingsButtonBackground()
    {
        _settingsButton.Background = _isSettingsButtonActive
            ? SettingsActiveBackground
            : Brushes.Transparent;
    }

    private void WireSettingsButtonPointerFeedback(Button button)
    {
        button.PointerEntered += (_, _) =>
        {
            if (!button.IsPressed)
            {
                button.Background = SettingsHoverBackground;
            }
        };
        button.PointerExited += (_, _) => UpdateSettingsButtonBackground();
        button.PointerPressed += (_, _) => button.Background = SettingsPressedBackground;
        button.PointerReleased += (_, _) =>
            button.Background = button.IsPointerOver || _isSettingsButtonActive
                ? SettingsHoverBackground
                : Brushes.Transparent;
    }

    /// <summary>
    /// Display-only status segment: icon + caption, no button chrome, cursor, or command.
    /// </summary>
    private static Control BuildStatusSegment(string iconKey, TextBlock text)
    {
        text.VerticalAlignment = VerticalAlignment.Center;
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingXs,
            VerticalAlignment = VerticalAlignment.Center,
            // Keep readable status spacing without button padding/hover.
            Margin = LayoutTokens.Symmetric(LayoutTokens.SpacingXs, LayoutTokens.SpacingXxs),
            Children =
            {
                IconFactory.Create(
                    iconKey,
                    (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
                    12),
                text
            }
        };
    }

    /// <summary>
    /// Interactive Settings control only. Command is bound in <see cref="WhenActivated"/>.
    /// </summary>
    private static Button BuildSettingsButton(Control content)
    {
        var button = new Button
        {
            Content = content,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            BorderThickness = LayoutTokens.NoneThickness,
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingXs, LayoutTokens.SpacingXxs),
            CornerRadius = LayoutTokens.RadiusSm,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        // Accessible identity for the sole interactive status-bar control.
        ToolTip.SetTip(button, "Settings");
        Avalonia.Automation.AutomationProperties.SetName(button, "Settings");
        return button;
    }

}
