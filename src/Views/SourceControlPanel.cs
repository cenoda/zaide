using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Zaide.Models;
using Zaide.ViewModels;

namespace Zaide.Views;

/// <summary>
/// Source Control panel view. Shows branch selector, change list,
/// staged section, and commit input. Uses static/demo data only.
/// </summary>
public class SourceControlPanel : ReactiveUserControl<SourceControlViewModel>
{
    private readonly ComboBox _branchSelector;
    private readonly ItemsControl _unstagedList;
    private readonly ItemsControl _stagedList;
    private readonly TextBox _commitInput;
    private readonly Button _commitButton;
    private readonly TextBlock _stagedHeader;
    private readonly TextBlock _unstagedHeader;

    public SourceControlPanel()
    {
        Background = (IBrush?)Application.Current!.Resources["SurfacePanelBrush"];

        // --- Header ---
        var header = new TextBlock
        {
            Text = "Source Control",
            Foreground = (IBrush?)Application.Current!.Resources["TextPrimaryBrush"],
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(12, 16, 12, 8)
        };

        // --- Branch Selector ---
        _branchSelector = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(12, 0, 12, 12),
            PlaceholderText = "Select branch",
            Background = new SolidColorBrush(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF)),
            Foreground = (IBrush?)Application.Current!.Resources["TextPrimaryBrush"],
            FontSize = 13
        };

        // --- Unstaged Changes Header ---
        _unstagedHeader = new TextBlock
        {
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
            FontSize = 12,
            Margin = new Thickness(12, 4, 12, 4)
        };

        // --- Unstaged Changes List ---
        _unstagedList = new ItemsControl
        {
            Margin = new Thickness(0)
        };
        _unstagedList.ItemTemplate = CreateChangeItemTemplate(isStaged: false);

        // --- Staged Section Header ---
        _stagedHeader = new TextBlock
        {
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
            FontSize = 12,
            Margin = new Thickness(12, 8, 12, 4)
        };

        // --- Staged Changes List ---
        _stagedList = new ItemsControl
        {
            Margin = new Thickness(0)
        };
        _stagedList.ItemTemplate = CreateChangeItemTemplate(isStaged: true);

        // --- Commit Input ---
        _commitInput = new TextBox
        {
            PlaceholderText = "Commit message...",
            AcceptsReturn = false,
            Height = 32,
            Margin = new Thickness(12, 8, 12, 4),
            Background = new SolidColorBrush(Color.FromArgb(0x0D, 0xFF, 0xFF, 0xFF)),
            Foreground = (IBrush?)Application.Current!.Resources["TextPrimaryBrush"],
            BorderThickness = new Thickness(0),
            FontSize = 13
        };

        // --- Commit Button ---
        _commitButton = new Button
        {
            Content = "Commit Staged",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(12, 4, 12, 16),
            Height = 30,
            Background = (IBrush?)Application.Current!.Resources["PrimaryAccentBrush"],
            Foreground = (IBrush?)Application.Current!.Resources["TextPrimaryBrush"],
            FontSize = 13,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };

        // --- Layout ---
        var scrollViewer = new ScrollViewer
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Children =
                {
                    header,
                    _branchSelector,
                    _unstagedHeader,
                    _unstagedList,
                    _stagedHeader,
                    _stagedList,
                    _commitInput,
                    _commitButton
                }
            }
        };

        Content = scrollViewer;

        // --- Reactive Bindings ---
        this.WhenActivated(d =>
        {
            // Bind branch selector items
            d.Add(this.WhenAnyValue(x => x.ViewModel)
                .Where(vm => vm is not null)
                .Subscribe(vm =>
                {
                    _branchSelector.ItemsSource = vm!.Branches;
                    _branchSelector.SelectedItem = vm.SelectedBranch;
                }));

            // Branch selection → ViewModel
            d.Add(Observable.FromEventPattern<SelectionChangedEventArgs>(
                    h => _branchSelector.SelectionChanged += h,
                    h => _branchSelector.SelectionChanged -= h)
                .Select(_ => _branchSelector.SelectedItem as GitBranch)
                .Where(b => b is not null)
                .Subscribe(b => ViewModel?.SelectBranchCommand.Execute(b!).Subscribe()));

            // Bind unstaged changes
            d.Add(this.WhenAnyValue(x => x.ViewModel)
                .Where(vm => vm is not null)
                .Subscribe(vm => _unstagedList.ItemsSource = vm!.UnstagedChanges));

            // Bind staged changes
            d.Add(this.WhenAnyValue(x => x.ViewModel)
                .Where(vm => vm is not null)
                .Subscribe(vm => _stagedList.ItemsSource = vm!.StagedChanges));

            // Update headers when counts change
            d.Add(this.WhenAnyValue(x => x.ViewModel!.UnstagedCount)
                .Subscribe(count =>
                    _unstagedHeader.Text = $"Changes ({count})"));

            d.Add(this.WhenAnyValue(x => x.ViewModel!.StagedCount)
                .Subscribe(count =>
                    _stagedHeader.Text = $"Staged ({count})"));

            // Commit message binding
            d.Add(this.Bind(ViewModel, vm => vm.CommitMessage, v => v._commitInput.Text));

            // Commit button
            d.Add(Observable.FromEventPattern<RoutedEventArgs>(
                    h => _commitButton.Click += h,
                    h => _commitButton.Click -= h)
                .InvokeCommand(ViewModel, vm => vm.CommitCommand));
        });
    }

    private FuncDataTemplate<FileChange> CreateChangeItemTemplate(bool isStaged)
    {
        return new FuncDataTemplate<FileChange>((change, _) =>
        {
            if (change is null) return null;

            // Status icon
            var (statusChar, statusColor) = change.ChangeType switch
            {
                GitChangeType.Added => ("A", "#28A745"),
                GitChangeType.Modified => ("M", "#FCBB47"),
                GitChangeType.Deleted => ("D", "#E05555"),
                _ => ("?", "#8B95A5")
            };

            var statusIcon = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(4),
                Child = new TextBlock
                {
                    Text = statusChar,
                    Foreground = new SolidColorBrush(Color.Parse(statusColor)),
                    FontSize = 11,
                    FontWeight = FontWeight.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            // File path
            var filePath = new TextBlock
            {
                Text = change.FilePath,
                Foreground = (IBrush?)Application.Current!.Resources["TextPrimaryBrush"],
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };

            // Stage/Unstage button
            var stageButton = new Button
            {
                Content = isStaged ? "−" : "+",
                Width = 20,
                Height = 20,
                FontSize = 12,
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
                BorderThickness = new Thickness(0),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = change
            };
            stageButton.Click += (_, _) =>
            {
                if (change is null) return;
                // Walk up to find the SourceControlPanel to get its ViewModel
                var parent = stageButton.Parent;
                while (parent is not null && parent is not SourceControlPanel)
                    parent = parent.Parent;
                var vm = (parent as SourceControlPanel)?.ViewModel;
                if (vm is null) return;
                if (isStaged)
                    vm.UnstageFileCommand.Execute(change).Subscribe();
                else
                    vm.StageFileCommand.Execute(change).Subscribe();
            };

            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(20) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(24) }
                },
                Margin = new Thickness(12, 4, 12, 4),
                Children =
                {
                    statusIcon,
                    filePath,
                    stageButton
                }
            };
            Grid.SetColumn(filePath, 1);
            Grid.SetColumn(stageButton, 2);

            return row;
        });
    }
}