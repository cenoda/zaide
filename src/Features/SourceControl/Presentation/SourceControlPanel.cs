using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Zaide.Features.SourceControl.Domain;
using Zaide.UI.DesignSystem;
using Zaide.App.Shell;
using Zaide.Features.Workspace.Presentation;

namespace Zaide.Features.SourceControl.Presentation;

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
    private readonly Button _stageAllButton;
    private readonly Button _unstageAllButton;
    private readonly TextBlock _commitErrorText;
    private readonly TextBlock _stagedHeader;
    private readonly TextBlock _unstagedHeader;
    private readonly TextBlock _statusMessage;

    public SourceControlPanel()
    {
        Background = (IBrush?)Avalonia.Application.Current!.Resources["SurfacePanelBrush"];

        // --- Header ---
        var title = TextStyles.Header("Source Control");

        var titleGroup = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingSm - LayoutTokens.SpacingXxs,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { title }
        };

        var refreshButton = new Button
        {
            Content = IconFactory.Create(
                "Icon.ArrowClockwise",
                (IBrush?)Avalonia.Application.Current!.Resources["TextSecondaryBrush"],
                16),
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
        ToolTip.SetTip(refreshButton, "Refresh source control");
        AutomationProperties.SetName(refreshButton, "Refresh source control");

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
            Background = ThemeBinding.GetBrush("OverlayHoverBrush"),
            Foreground = (IBrush?)Avalonia.Application.Current!.Resources["TextPrimaryBrush"],
            FontSize = 13
        };

        // --- Unstaged Changes Header (caption + Stage All) ---
        _unstagedHeader = TextStyles.Caption("Unstaged Changes");
        _unstagedHeader.VerticalAlignment = VerticalAlignment.Center;

        _stageAllButton = new Button
        {
            Content = "Stage All",
            FontSize = 11,
            Padding = LayoutTokens.Inset(LayoutTokens.SpacingSm, LayoutTokens.SpacingXxs, LayoutTokens.SpacingSm, LayoutTokens.SpacingXxs),
            Background = Brushes.Transparent,
            Foreground = (IBrush?)Avalonia.Application.Current!.Resources["TextSecondaryBrush"],
            BorderThickness = LayoutTokens.NoneThickness,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            // Bound to UnstagedCount > 0 on activation; hidden until then.
            IsVisible = false
        };

        var unstagedHeaderRow = new Grid
        {
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingMd, LayoutTokens.SpacingXs, LayoutTokens.SpacingMd, LayoutTokens.SpacingXs),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Children = { _unstagedHeader, _stageAllButton }
        };
        Grid.SetColumn(_unstagedHeader, 0);
        Grid.SetColumn(_stageAllButton, 1);

        // --- Status Message (non-repo / error notice; hidden on success) ---
        _statusMessage = TextStyles.Body("");
        _statusMessage.Margin = LayoutTokens.Inset(LayoutTokens.SpacingMd, 0, LayoutTokens.SpacingMd, LayoutTokens.SpacingSm);
        _statusMessage.Foreground = (IBrush?)Avalonia.Application.Current!.Resources["TextSecondaryBrush"];
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

        // --- Staged Section Header (caption + Unstage All) ---
        _stagedHeader = TextStyles.Caption("Staged Changes");
        _stagedHeader.VerticalAlignment = VerticalAlignment.Center;

        _unstageAllButton = new Button
        {
            Content = "Unstage All",
            FontSize = 11,
            Padding = LayoutTokens.Inset(LayoutTokens.SpacingSm, LayoutTokens.SpacingXxs, LayoutTokens.SpacingSm, LayoutTokens.SpacingXxs),
            Background = Brushes.Transparent,
            Foreground = (IBrush?)Avalonia.Application.Current!.Resources["TextSecondaryBrush"],
            BorderThickness = LayoutTokens.NoneThickness,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            // Bound to StagedCount > 0 on activation; hidden until then.
            IsVisible = false
        };

        var stagedHeaderRow = new Grid
        {
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingMd, LayoutTokens.SpacingSm, LayoutTokens.SpacingMd, LayoutTokens.SpacingXs),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Children = { _stagedHeader, _unstageAllButton }
        };
        Grid.SetColumn(_stagedHeader, 0);
        Grid.SetColumn(_unstageAllButton, 1);

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

        // --- Commit Input ---
        _commitInput = new TextBox
        {
            PlaceholderText = "Commit message...",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 32,
            MaxHeight = 120,
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingMd, LayoutTokens.SpacingSm, LayoutTokens.SpacingMd, LayoutTokens.SpacingXs),
            Background = ThemeBinding.GetBrush("SurfaceRaised1Brush"),
            Foreground = (IBrush?)Avalonia.Application.Current!.Resources["TextPrimaryBrush"],
            BorderThickness = LayoutTokens.NoneThickness,
            FontSize = 13
        };

        // --- Primary Action Button (Commit or Push) ---
        _commitButton = new Button
        {
            Content = "Commit",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingMd, LayoutTokens.SpacingXs, LayoutTokens.SpacingMd, LayoutTokens.SpacingLg),
            Height = 30,
            Background = (IBrush?)Avalonia.Application.Current!.Resources["PrimaryAccentBrush"],
            Foreground = (IBrush?)Avalonia.Application.Current!.Resources["TextPrimaryBrush"],
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
                    unstagedHeaderRow,
                    _unstagedList,
                    stagedHeaderRow,
                    _stagedList,
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
            // Bind branch selector items once; the collection mutates in place on refresh.
            d.Add(this.WhenAnyValue(x => x.ViewModel)
                .Where(vm => vm is not null)
                .Subscribe(vm => _branchSelector.ItemsSource = vm!.Branches));

            // Keep the ComboBox selection in sync whenever the ViewModel updates
            // SelectedBranch (e.g. after snapshot refresh, commit, or stage). A
            // one-time assignment on ViewModel attach leaves SelectedItem null once
            // Branches.Clear() runs during refresh, which shows the placeholder.
            d.Add(this.WhenAnyValue(x => x.ViewModel!.SelectedBranch)
                .Subscribe(branch => _branchSelector.SelectedItem = branch));

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

            // --- ListBox selection (F12a) ---
            // Do not two-way bind one SelectedFileChange to both ListBoxes: a
            // staged object is never in the unstaged ItemsSource (and vice
            // versa), so dual bind churns selection and can leave multi-row
            // selected chrome. Project VM selection into the owning list only;
            // user clicks set VM selection and clear the sibling list.

            d.Add(this.WhenAnyValue(x => x.ViewModel!.SelectedFileChange)
                .Subscribe(ApplyExclusiveListSelection));

            d.Add(Observable.FromEventPattern<SelectionChangedEventArgs>(
                    h => _unstagedList.SelectionChanged += h,
                    h => _unstagedList.SelectionChanged -= h)
                .Select(_ => _unstagedList.SelectedItem as FileChange)
                .Where(f => f is not null)
                .Subscribe(f =>
                {
                    if (_stagedList.SelectedItem is not null)
                        _stagedList.SelectedItem = null;
                    ViewModel?.SelectFileCommand.Execute(f!).Subscribe();
                }));

            d.Add(Observable.FromEventPattern<SelectionChangedEventArgs>(
                    h => _stagedList.SelectionChanged += h,
                    h => _stagedList.SelectionChanged -= h)
                .Select(_ => _stagedList.SelectedItem as FileChange)
                .Where(f => f is not null)
                .Subscribe(f =>
                {
                    if (_unstagedList.SelectedItem is not null)
                        _unstagedList.SelectedItem = null;
                    ViewModel?.SelectFileCommand.Execute(f!).Subscribe();
                }));

            // Surface non-repo / error notice; hidden on success
            d.Add(this.WhenAnyValue(x => x.ViewModel!.StatusMessage)
                .Subscribe(msg =>
                {
                    _statusMessage.Text = msg ?? string.Empty;
                    _statusMessage.IsVisible = !string.IsNullOrEmpty(msg);
                }));

            // Update headers when counts change. Stage All / Unstage All are
            // shown only when the matching list is non-empty; CanExecute
            // separately disables them while a bulk op is in flight.
            d.Add(this.WhenAnyValue(x => x.ViewModel!.UnstagedCount)
                .Subscribe(count =>
                {
                    _unstagedHeader.Text = $"Changes ({count})";
                    _stageAllButton.IsVisible = count > 0;
                }));

            d.Add(this.WhenAnyValue(x => x.ViewModel!.StagedCount)
                .Subscribe(count =>
                {
                    _stagedHeader.Text = $"Staged ({count})";
                    _unstageAllButton.IsVisible = count > 0;
                }));

            // Stage All: project click to Unit so InvokeCommand matches StageAllCommand.
            d.Add(Observable.FromEventPattern<RoutedEventArgs>(
                    h => _stageAllButton.Click += h,
                    h => _stageAllButton.Click -= h)
                .Select(_ => Unit.Default)
                .InvokeCommand(ViewModel, vm => vm.StageAllCommand));
            d.Add(this.WhenAnyObservable(x => x.ViewModel!.StageAllCommand.CanExecute)
                .Subscribe(can => _stageAllButton.IsEnabled = can));

            // Unstage All: mirror Stage All wiring against UnstageAllCommand.
            d.Add(Observable.FromEventPattern<RoutedEventArgs>(
                    h => _unstageAllButton.Click += h,
                    h => _unstageAllButton.Click -= h)
                .Select(_ => Unit.Default)
                .InvokeCommand(ViewModel, vm => vm.UnstageAllCommand));
            d.Add(this.WhenAnyObservable(x => x.ViewModel!.UnstageAllCommand.CanExecute)
                .Subscribe(can => _unstageAllButton.IsEnabled = can));

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
                        _commitErrorText.Foreground = ThemeBinding.GetBrush("DangerBrush");
                        _commitErrorText.Text = err;
                        _commitErrorText.IsVisible = true;
                    }
                    else if (!string.IsNullOrEmpty(notice))
                    {
                        _commitErrorText.Foreground =
                            (IBrush?)Avalonia.Application.Current!.Resources["TextSecondaryBrush"];
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

    /// <summary>
    /// Shows selection chrome only on the list that owns the selected change.
    /// The sibling list is cleared so at most one ListBox has a SelectedItem.
    /// </summary>
    private void ApplyExclusiveListSelection(FileChange? file)
    {
        if (file is null)
        {
            if (_unstagedList.SelectedItem is not null)
                _unstagedList.SelectedItem = null;
            if (_stagedList.SelectedItem is not null)
                _stagedList.SelectedItem = null;
            return;
        }

        if (file.IsStaged)
        {
            if (!ReferenceEquals(_stagedList.SelectedItem, file))
                _stagedList.SelectedItem = file;
            if (_unstagedList.SelectedItem is not null)
                _unstagedList.SelectedItem = null;
        }
        else
        {
            if (!ReferenceEquals(_unstagedList.SelectedItem, file))
                _unstagedList.SelectedItem = file;
            if (_stagedList.SelectedItem is not null)
                _stagedList.SelectedItem = null;
        }
    }

    private Style CreateChangeListItemStyle()
    {
        var style = new Style(s => s.OfType<ListBoxItem>());
        style.Setters.Add(new Setter(ListBoxItem.PaddingProperty, LayoutTokens.NoneThickness));
        style.Setters.Add(new Setter(ListBoxItem.MinHeightProperty, 24.0));
        return style;
    }

    private FuncDataTemplate<FileChange> CreateChangeItemTemplate(bool isStaged)
    {
        return new FuncDataTemplate<FileChange>((change, _) =>
        {
            if (change is null) return null;

            // Status icon
            var (statusChar, statusBrush) = change.ChangeType switch
            {
                GitChangeType.Added => ("A", ThemeBinding.GetBrush("SuccessBrush")),
                GitChangeType.Modified => ("M", ThemeBinding.GetBrush("WarningBrush")),
                GitChangeType.Deleted => ("D", ThemeBinding.GetBrush("DangerBrush")),
                _ => ("?", ThemeBinding.GetBrush("TextSecondaryBrush"))
            };

            var statusText = TextStyles.Caption(statusChar);
            statusText.FontWeight = FontWeight.Bold;
            statusText.Foreground = statusBrush;
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
                (IBrush?)Avalonia.Application.Current!.Resources["TextSecondaryBrush"],
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
                Foreground = (IBrush?)Avalonia.Application.Current!.Resources["TextSecondaryBrush"],
                BorderThickness = LayoutTokens.NoneThickness,
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
