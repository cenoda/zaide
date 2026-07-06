using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
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
public class StatusBar : ReactiveUserControl<MainWindowViewModel>
{
    private static readonly ICommand StatusSegmentCommand = ReactiveCommand.Create(() => { });
    private readonly TextBlock _caretText;
    private readonly TextBlock _languageText;
    private readonly TextBlock _projectText;
    private readonly TextBlock _branchText;

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
        _caretText = TextStyles.Caption("");

        // Language (dynamic text)
        _languageText = TextStyles.Caption("C#");

        // Project (dynamic text)
        _projectText = TextStyles.Caption("Zaide");

        // Branch (dynamic text)
        _branchText = TextStyles.Caption("master");

        // AI model (right-aligned, static text)
        var modelText = TextStyles.Caption("powered by Avisnis 12");
        modelText.HorizontalAlignment = HorizontalAlignment.Right;
        modelText.Margin = LayoutTokens.Inset(0, 0, LayoutTokens.SpacingMd, 0);
        modelText.Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"];

        // Left-aligned stack: app name caret language project branch
        var appNameButton = BuildStatusSegmentButton(new StackPanel
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
                appNameButton,
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
                modelText
            }
        };
        Grid.SetColumn(leftStack, 0);
        Grid.SetColumn(modelText, 1);

        Content = layout;

        // --- Reactive Bindings ---
        this.WhenActivated(d =>
        {
            if (ViewModel is null) return;

            // Caret position from EditorViewModel active tab
            d.Add(ViewModel.WhenAnyValue(x => x.EditorTabs.ActiveTab)
                .Select(tab => tab is not null
                    ? tab.WhenAnyValue(t => t.CaretLine, t => t.CaretColumn,
                        (line, col) => $"Ln {line}, Col {col}")
                    : Observable.Return("Ln 1, Col 1"))
                .Switch()
                .Subscribe(text => _caretText.Text = text));

            // Language from active tab file extension
            d.Add(ViewModel.WhenAnyValue(x => x.EditorTabs.ActiveTab)
                .Select(tab => tab is not null
                    ? GetLanguageFromFilePath(tab.FilePath)
                    : "—")
                .Subscribe(lang => _languageText.Text = lang));

            // Project name from Workspace
            d.Add(ViewModel.WhenAnyValue(x => x.WorkspaceProjectName)
                .Subscribe(name => _projectText.Text = name ?? "Zaide"));

            // Branch from SourceControlViewModel
            d.Add(ViewModel.WhenAnyValue(x => x.SourceControlViewModel.CurrentBranchName)
                .Subscribe(branch => _branchText.Text = branch));
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

    private static Button BuildStatusSegmentButton(Control content)
    {
        var button = new Button
        {
            Content = content,
            Command = StatusSegmentCommand,
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

    private static string GetLanguageFromFilePath(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return "—";

        var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".cs" => "C#",
            ".ts" => "TypeScript",
            ".js" => "JavaScript",
            ".json" => "JSON",
            ".md" => "Markdown",
            ".xml" => "XML",
            ".html" => "HTML",
            ".css" => "CSS",
            ".py" => "Python",
            _ => ext.TrimStart('.').ToUpperInvariant()
        };
    }
}
