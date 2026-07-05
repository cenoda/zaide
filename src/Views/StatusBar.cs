using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Zaide.ViewModels;

namespace Zaide.Views;

/// <summary>
/// Status bar at the very bottom of the window.
/// Shows app name, cursor position, language, project, branch, and AI model.
/// Thin bar (~24px height), full width.
/// </summary>
public class StatusBar : ReactiveUserControl<MainWindowViewModel>
{
    private readonly TextBlock _caretText;
    private readonly TextBlock _languageText;
    private readonly TextBlock _projectText;
    private readonly TextBlock _branchText;

    public StatusBar()
    {
        Height = 24;
        Background = (IBrush?)Application.Current!.Resources["SurfaceBaseBrush"];

        // App name — PrimaryAccentBrush
        var appNameText = new TextBlock
        {
            Text = "\u2699 Zaide",
            Foreground = (IBrush?)Application.Current!.Resources["PrimaryAccentBrush"],
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };

        // Caret position
        _caretText = new TextBlock
        {
            Text = "Ln 1, Col 1",
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Language
        _languageText = new TextBlock
        {
            Text = "C#",
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Project
        _projectText = new TextBlock
        {
            Text = "Zaide",
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Branch
        _branchText = new TextBlock
        {
            Text = "master",
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };

        // AI model (right-aligned)
        var modelText = new TextBlock
        {
            Text = "powered by Avisnis 12",
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 12, 0)
        };

        // Separator
        TextBlock Separator() => new()
        {
            Text = "│",
            Foreground = (IBrush?)Application.Current!.Resources["SeparatorBrush"],
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 8, 0)
        };

        // Left-aligned stack: app name | caret | language | project | branch
        var leftStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                appNameText,
                Separator(),
                _caretText,
                Separator(),
                _languageText,
                Separator(),
                _projectText,
                Separator(),
                _branchText
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