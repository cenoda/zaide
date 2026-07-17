using System;
using System.Windows.Input;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
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
/// </summary>
public class StatusBar : ReactiveUserControl<StatusBarViewModel>
{
    internal static string? FormatConfiguredModel(string? model) =>
        string.IsNullOrWhiteSpace(model) ? null : $"configured: {model}";
    private static readonly ICommand StatusSegmentCommand = ReactiveCommand.Create(() => { });
    private readonly TextBlock _caretText = TextStyles.Caption("");
    private readonly TextBlock _languageText = TextStyles.Caption("—");
    private readonly TextBlock _languageIntelligenceText = TextStyles.Caption("");
    private readonly Button _languageIntelligenceButton;
    private readonly TextBlock _projectText = TextStyles.Caption("Zaide");
    private readonly TextBlock _branchText = TextStyles.Caption("");
    private readonly TextBlock _documentText = TextStyles.Caption("—");
    private readonly TextBlock _statusMessageText = TextStyles.Caption("");
    private readonly TextBlock _modelText;
    private readonly Button _settingsButton;
    private readonly Viewbox _settingsIcon;
    private readonly TextBlock _settingsAppNameText;
    private readonly SolidColorBrush _settingsActiveBackground = new(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF));
    private readonly SolidColorBrush _settingsHoverBackground = new(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF));
    private readonly SolidColorBrush _settingsPressedBackground = new(Color.FromArgb(0x1E, 0xFF, 0xFF, 0xFF));
    private bool _isSettingsButtonActive;

    public StatusBar()
    {
        Height = 24;
        Background = (IBrush?)Application.Current!.Resources["SurfaceBaseBrush"];

        _settingsIcon = IconFactory.Create(
            "Icon.Config",
            (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
            14);

        _settingsAppNameText = TextStyles.Brand("Zaide");
        _settingsAppNameText.VerticalAlignment = VerticalAlignment.Center;
        _settingsAppNameText.Margin = LayoutTokens.Inset(LayoutTokens.SpacingXs, 0, 0, 0);
        ApplySettingsButtonVisualState(false);

        // Configured model (far-right caption). Centered like button segments so it
        // shares the same vertical midline as the rest of the 24px status bar.
        _modelText = TextStyles.Caption("");
        _modelText.HorizontalAlignment = HorizontalAlignment.Right;
        _modelText.VerticalAlignment = VerticalAlignment.Center;
        _modelText.Margin = LayoutTokens.Inset(0, 0, LayoutTokens.SpacingMd, 0);
        _modelText.Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"];

        // Left-aligned stack: app name caret language project branch
        _settingsButton = BuildStatusSegmentButton(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _settingsIcon, _settingsAppNameText }
        });
        WireSettingsButtonPointerFeedback(_settingsButton);

        _languageIntelligenceText.Foreground =
            (IBrush?)Application.Current!.Resources["TextSecondaryBrush"];
        _languageIntelligenceButton = BuildStatusSegmentButton("Icon.Code", _languageIntelligenceText);
        _languageIntelligenceButton.IsVisible = false;

        // Transient "Opened: …" / save/search feedback. Same center rule as segment
        // buttons: bare TextBlocks default to Stretch and paint text at the top.
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
                BuildStatusSegmentButton("Icon.Text", _documentText),
                BuildStatusSegmentButton("Icon.Selection", _caretText),
                BuildStatusSegmentButton("Icon.Code", _languageText),
                _languageIntelligenceButton,
                BuildStatusSegmentButton("Icon.Project", _projectText),
                BuildStatusSegmentButton("Icon.GitBranch", _branchText),
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
                    _languageIntelligenceButton.IsVisible = !string.IsNullOrEmpty(text);
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
            ? _settingsActiveBackground
            : Brushes.Transparent;
    }

    private void WireSettingsButtonPointerFeedback(Button button)
    {
        button.PointerEntered += (_, _) =>
        {
            if (!button.IsPressed)
            {
                button.Background = _settingsHoverBackground;
            }
        };
        button.PointerExited += (_, _) => UpdateSettingsButtonBackground();
        button.PointerPressed += (_, _) => button.Background = _settingsPressedBackground;
        button.PointerReleased += (_, _) =>
            button.Background = button.IsPointerOver || _isSettingsButtonActive
                ? _settingsHoverBackground
                : Brushes.Transparent;
    }

    private static Button BuildStatusSegmentButton(string iconKey, TextBlock text)
    {
        return BuildStatusSegmentButton(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingXs,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                IconFactory.Create(
                    iconKey,
                    (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
                    12),
                text
            }
        });
    }

    private static Button BuildStatusSegmentButton(Control content) =>
        BuildStatusSegmentButton(content, StatusSegmentCommand);

    private static Button BuildStatusSegmentButton(Control content, ICommand? command)
    {
        var button = new Button
        {
            Content = content,
            Command = command,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            BorderThickness = LayoutTokens.NoneThickness,
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingXs, LayoutTokens.SpacingXxs),
            CornerRadius = LayoutTokens.RadiusSm,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };

        var hoverBrush = new SolidColorBrush(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF));
        var pressedBrush = new SolidColorBrush(Color.FromArgb(0x1E, 0xFF, 0xFF, 0xFF));

        button.PointerEntered += (_, _) =>
        {
            if (!button.IsPressed)
            {
                button.Background = hoverBrush;
            }
        };
        button.PointerExited += (_, _) => button.Background = Brushes.Transparent;
        button.PointerPressed += (_, _) => button.Background = pressedBrush;
        button.PointerReleased += (_, _) =>
            button.Background = button.IsPointerOver ? hoverBrush : Brushes.Transparent;

        return button;
    }

}
