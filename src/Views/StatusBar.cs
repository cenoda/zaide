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
using Zaide.ViewModels;
using Zaide.Styles;

namespace Zaide.Views;

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
    private readonly TextBlock _projectText = TextStyles.Caption("Zaide");
    private readonly TextBlock _branchText = TextStyles.Caption("");
    private readonly TextBlock _modelText;
    private readonly Button _settingsButton;

    public StatusBar()
    {
        Height = 24;
        Background = (IBrush?)Application.Current!.Resources["SurfaceBaseBrush"];

        // App name — PrimaryAccentBrush
        var appNameIcon = IconFactory.Create(
            "Icon.Config",
            (IBrush?)Application.Current!.Resources["PrimaryAccentBrush"],
            14);

        var appNameText = TextStyles.Brand("Zaide");
        appNameText.VerticalAlignment = VerticalAlignment.Center;
        appNameText.Margin = LayoutTokens.Inset(LayoutTokens.SpacingXs, 0, 0, 0);

        // Caret position (dynamic text)
        _modelText = TextStyles.Caption("");
        _modelText.HorizontalAlignment = HorizontalAlignment.Right;
        _modelText.Margin = LayoutTokens.Inset(0, 0, LayoutTokens.SpacingMd, 0);
        _modelText.Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"];

        // Left-aligned stack: app name caret language project branch
        _settingsButton = BuildStatusSegmentButton(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { appNameIcon, appNameText }
        });

        var leftStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = LayoutTokens.SpacingMd,
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingMd, 0, 0, 0),
            Children =
            {
                _settingsButton,
                BuildStatusSegmentButton("Icon.Selection", _caretText),
                BuildStatusSegmentButton("Icon.Code", _languageText),
                BuildStatusSegmentButton("Icon.Project", _projectText),
                BuildStatusSegmentButton("Icon.GitBranch", _branchText)
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
            d.Add(ViewModel.WhenAnyValue(x => x.ProjectText).Subscribe(Observer.Create<string>(text => _projectText.Text = text)));
            d.Add(ViewModel.WhenAnyValue(x => x.BranchText).Subscribe(Observer.Create<string>(text => _branchText.Text = text)));
            d.Add(ViewModel.WhenAnyValue(x => x.ConfiguredModel)
                .Subscribe(Observer.Create<string?>(model =>
                {
                    _modelText.Text = FormatConfiguredModel(model) ?? "";
                    _modelText.IsVisible = model is not null;
                })));
        });
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
