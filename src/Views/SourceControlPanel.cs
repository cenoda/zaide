using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Zaide.Models;
using Zaide.ViewModels;
using Zaide.Styles;

namespace Zaide.Views;

/// <summary>
/// Source Control panel view. Shows branch selector, change list,
/// staged section, and commit input. Data is loaded from the live
/// repository via <see cref="SourceControlViewModel"/> and refreshes
/// on explicit user action or workspace-open.
/// </summary>
public class SourceControlPanel : ReactiveUserControl<SourceControlViewModel>
{
    private readonly ComboBox _branchSelector;
    private readonly ListBox _unstagedList;
    private readonly ListBox _stagedList;
    private readonly TextBox _commitInput;
    private readonly Button _commitButton;
    private readonly TextBlock _commitErrorText;
    private readonly TextBlock _stagedHeader;
    private readonly TextBlock _unstagedHeader;
    private readonly TextBlock _statusMessage;
    private readonly Border _diffContainer;
    private readonly TextBlock _diffTextBlock;

    public SourceControlPanel()
    {
        Background = (IBrush?)Application.Current!.Resources["SurfacePanelBrush"];

        // --- Header ---
        var branchIcon = IconFactory.Create(
            "Icon.GitBranch",
            (IBrush?)Application.Current!.Resources["TextPrimaryBrush"],
            14);
        var title = TextStyles.Header("Source Control");

        var titleGroup = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingSm - LayoutTokens.SpacingXxs,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { branchIcon, title }
        };

        var refreshButton = new Button
        {
            Content = IconFactory.Create(
                "Icon.ArrowClockwise",
                (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
                14),
            Background = Brushes.Transparent,
            BorderThickness = LayoutTokens.NoneThickness,
            Padding = LayoutTokens.NoneThickness,
            CornerRadius = LayoutTokens.RadiusSm,
            Width = 24,
            Height = 24,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };

        var header = new Grid
        {
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingMd, LayoutTokens.SpacingLg, LayoutTokens.SpacingMd, LayoutTokens.SpacingSm),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Children = { titleGroup, refreshButton }
        };
        Grid.SetColumn(titleGroup, 0);
        Grid.SetColumn(refreshButton, 2);

        // --- Branch Selector ---
        _branchSelector = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingMd, 0, LayoutTokens.SpacingMd, LayoutTokens.SpacingMd),
            PlaceholderText = "Select branch",
            Background = new SolidColorBrush(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF)),
            Foreground = (IBrush?)Application.Current!.Resources["TextPrimaryBrush"],
            FontSize = 13
        };

        // --- Unstaged Changes Header ---
        _unstagedHeader = TextStyles.Caption("Unstaged Changes");
        _unstagedHeader.Margin = LayoutTokens.Inset(LayoutTokens.SpacingMd, LayoutTokens.SpacingXs, LayoutTokens.SpacingMd, LayoutTokens.SpacingXs);

        // --- Status Message (non-repo / error notice; hidden on success) ---
        _statusMessage = TextStyles.Body("");
        _statusMessage.Margin = LayoutTokens.Inset(LayoutTokens.SpacingMd, 0, LayoutTokens.SpacingMd, LayoutTokens.SpacingSm);
        _statusMessage.Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"];
        _statusMessage.IsVisible = false;
        _statusMessage.TextWrapping = Avalonia.Media.TextWrapping.Wrap;

        // --- Unstaged Changes List ---
        var changeListItemStyle = CreateChangeListItemStyle();

        _unstagedList = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = LayoutTokens.NoneThickness,
            SelectionMode = SelectionMode.Single,
            Margin = LayoutTokens.NoneThickness
        };
        _unstagedList.Styles.Add(changeListItemStyle);
        _unstagedList.ItemTemplate = CreateChangeItemTemplate(isStaged: false);

        // --- Staged Section Header ---
        _stagedHeader = TextStyles.Caption("Staged Changes");
        _stagedHeader.Margin = LayoutTokens.Inset(LayoutTokens.SpacingMd, LayoutTokens.SpacingSm, LayoutTokens.SpacingMd, LayoutTokens.SpacingXs);

        // --- Staged Changes List ---
        _stagedList = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = LayoutTokens.NoneThickness,
            SelectionMode = SelectionMode.Single,
            Margin = LayoutTokens.NoneThickness
        };
        _stagedList.Styles.Add(CreateChangeListItemStyle());
        _stagedList.ItemTemplate = CreateChangeItemTemplate(isStaged: true);

        // --- Diff Surface (inline unified diff, shown on file selection) ---
        _diffTextBlock = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Foreground = (IBrush?)Application.Current!.Resources["TextPrimaryBrush"],
            TextWrapping = TextWrapping.NoWrap
        };

        var diffScrollViewer = new ScrollViewer
        {
            Content = _diffTextBlock,
            MaxHeight = 300,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Margin = LayoutTokens.Uniform(LayoutTokens.SpacingSm)
        };

        _diffContainer = new Border
        {
            Child = diffScrollViewer,
            Background = new SolidColorBrush(Color.FromArgb(0x08, 0xFF, 0xFF, 0xFF)),
            BorderThickness = LayoutTokens.NoneThickness,
            CornerRadius = LayoutTokens.RadiusSm,
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingMd, LayoutTokens.SpacingXs, LayoutTokens.SpacingMd, LayoutTokens.SpacingXs),
            IsVisible = false
        };

        // --- Commit Input ---
        _commitInput = new TextBox
        {
            PlaceholderText = "Commit message...",
            AcceptsReturn = false,
            Height = 32,
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingMd, LayoutTokens.SpacingSm, LayoutTokens.SpacingMd, LayoutTokens.SpacingXs),
            Background = new SolidColorBrush(Color.FromArgb(0x0D, 0xFF, 0xFF, 0xFF)),
            Foreground = (IBrush?)Application.Current!.Resources["TextPrimaryBrush"],
            BorderThickness = new Thickness(0),
            FontSize = 13
        };

        // --- Primary Action Button (Commit or Push) ---
        _commitButton = new Button
        {
            Content = "Commit",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingMd, LayoutTokens.SpacingXs, LayoutTokens.SpacingMd, LayoutTokens.SpacingLg),
            Height = 30,
            Background = (IBrush?)Application.Current!.Resources["PrimaryAccentBrush"],
            Foreground = (IBrush?)Application.Current!.Resources["TextPrimaryBrush"],
            FontSize = 13,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };

        // --- Primary action feedback (errors or brief success notice) ---
        _commitErrorText = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingMd, 0, LayoutTokens.SpacingMd, LayoutTokens.SpacingSm),
            IsVisible = false
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
                    _statusMessage,
                    _unstagedHeader,
                    _unstagedList,
                    _stagedHeader,
                    _stagedList,
                    _diffContainer,
                    _commitInput,
                    _commitButton,
                    _commitErrorText
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

            // Explicit user refresh (reuses the orchestrator seam). Project the
            // event to Unit so it matches RefreshCommand's parameter type;
            // InvokeCommand otherwise forwards the EventPattern as the command
            // parameter and throws at execution time.
            d.Add(Observable.FromEventPattern<RoutedEventArgs>(
                    h => refreshButton.Click += h,
                    h => refreshButton.Click -= h)
                .Select(_ => Unit.Default)
                .InvokeCommand(ViewModel, vm => vm.RefreshCommand));

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

            // --- ListBox selection bindings ---

            // Two-way bind ListBox SelectedItem ↔ ViewModel SelectedFileChange for
            // visual selection sync in both directions.
            d.Add(this.Bind(ViewModel, vm => vm.SelectedFileChange, v => v._unstagedList.SelectedItem));
            d.Add(this.Bind(ViewModel, vm => vm.SelectedFileChange, v => v._stagedList.SelectedItem));

            // User-initiated selection triggers diff loading via SelectFileCommand.
            d.Add(Observable.FromEventPattern<SelectionChangedEventArgs>(
                    h => _unstagedList.SelectionChanged += h,
                    h => _unstagedList.SelectionChanged -= h)
                .Select(_ => _unstagedList.SelectedItem as FileChange)
                .Where(f => f is not null)
                .Subscribe(f => ViewModel?.SelectFileCommand.Execute(f!).Subscribe()));

            d.Add(Observable.FromEventPattern<SelectionChangedEventArgs>(
                    h => _stagedList.SelectionChanged += h,
                    h => _stagedList.SelectionChanged -= h)
                .Select(_ => _stagedList.SelectedItem as FileChange)
                .Where(f => f is not null)
                .Subscribe(f => ViewModel?.SelectFileCommand.Execute(f!).Subscribe()));

            // --- Diff surface visibility and content ---
            d.Add(this.WhenAnyValue(x => x.ViewModel!.CurrentDiff)
                .Subscribe(diff =>
                {
                    _diffContainer.IsVisible = diff != null;
                    if (diff == null)
                    {
                        _diffTextBlock.Text = string.Empty;
                    }
                    else if (diff.IsBinary)
                    {
                        _diffTextBlock.Text = "Binary file \u2014 diff not available";
                    }
                    else
                    {
                        _diffTextBlock.Text = diff.DiffText ?? string.Empty;
                    }
                }));

            // Surface non-repo / error notice; hidden on success
            d.Add(this.WhenAnyValue(x => x.ViewModel!.StatusMessage)
                .Subscribe(msg =>
                {
                    _statusMessage.Text = msg ?? string.Empty;
                    _statusMessage.IsVisible = !string.IsNullOrEmpty(msg);
                }));

            // Update headers when counts change
            d.Add(this.WhenAnyValue(x => x.ViewModel!.UnstagedCount)
                .Subscribe(count =>
                    _unstagedHeader.Text = $"Changes ({count})"));

            d.Add(this.WhenAnyValue(x => x.ViewModel!.StagedCount)
                .Subscribe(count =>
                    _stagedHeader.Text = $"Staged ({count})"));

            // Commit message binding
            d.Add(this.Bind(ViewModel, vm => vm.CommitMessage, v => v._commitInput.Text));

            d.Add(this.WhenAnyValue(x => x.ViewModel!.PrimaryActionLabel)
                .Subscribe(label => _commitButton.Content = label));

            // Primary action button. Project the event to Unit so it matches
            // PrimaryActionCommand's parameter type (see refresh button note above).
            d.Add(Observable.FromEventPattern<RoutedEventArgs>(
                    h => _commitButton.Click += h,
                    h => _commitButton.Click -= h)
                .Select(_ => Unit.Default)
                .InvokeCommand(ViewModel, vm => vm.PrimaryActionCommand));

            d.Add(this.WhenAnyValue(
                    x => x.ViewModel!.CommitError,
                    x => x.ViewModel!.PushError,
                    x => x.ViewModel!.ActionNotice)
                .Subscribe(tuple =>
                {
                    var err = tuple.Item1 ?? tuple.Item2;
                    var notice = tuple.Item3;
                    if (!string.IsNullOrEmpty(err))
                    {
                        _commitErrorText.Foreground = new SolidColorBrush(Color.Parse("#E05555"));
                        _commitErrorText.Text = err;
                        _commitErrorText.IsVisible = true;
                    }
                    else if (!string.IsNullOrEmpty(notice))
                    {
                        _commitErrorText.Foreground =
                            (IBrush?)Application.Current!.Resources["TextSecondaryBrush"];
                        _commitErrorText.Text = notice;
                        _commitErrorText.IsVisible = true;
                    }
                    else
                    {
                        _commitErrorText.Text = string.Empty;
                        _commitErrorText.IsVisible = false;
                    }
                }));
        });
    }

    private Style CreateChangeListItemStyle()
    {
        var style = new Style(s => s.OfType<ListBoxItem>());
        style.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(0)));
        style.Setters.Add(new Setter(ListBoxItem.MinHeightProperty, 24.0));
        return style;
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

            var statusText = TextStyles.Caption(statusChar);
            statusText.FontWeight = FontWeight.Bold;
            statusText.Foreground = new SolidColorBrush(Color.Parse(statusColor));
            statusText.HorizontalAlignment = HorizontalAlignment.Center;
            statusText.VerticalAlignment = VerticalAlignment.Center;

            var statusIcon = new Border
            {
                Width = 16,
                Height = 16,
                CornerRadius = LayoutTokens.RadiusSm,
                Child = statusText
            };

            var fileIcon = IconFactory.Create(
                FileIconKeyResolver.GetIconKey(change.FilePath),
                (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
                12);

            // File path
            var filePath = TextStyles.Body(change.FilePath);
            filePath.VerticalAlignment = VerticalAlignment.Center;
            filePath.Margin = LayoutTokens.Inset(LayoutTokens.SpacingSm - LayoutTokens.SpacingXxs, 0, 0, 0);

            // Stage/Unstage button
            var stageButton = new Button
            {
                Content = isStaged ? "−" : "+",
                Width = 16,
                Height = 16,
                FontSize = 12,
                Padding = LayoutTokens.NoneThickness,
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
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(24) }
                },
                Margin = LayoutTokens.Inset(LayoutTokens.SpacingSm, 0, LayoutTokens.SpacingSm, 0),
                MinHeight = 24,
                Children =
                {
                    statusIcon,
                    fileIcon,
                    filePath,
                    stageButton
                }
            };
            Grid.SetColumn(fileIcon, 1);
            Grid.SetColumn(filePath, 2);
            Grid.SetColumn(stageButton, 3);

            return row;
        });
    }
}
