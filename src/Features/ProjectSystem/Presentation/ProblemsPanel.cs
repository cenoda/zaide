using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Zaide.UI.DesignSystem;
using Zaide.Features.ProjectSystem.Domain;

namespace Zaide.Features.ProjectSystem.Presentation;

/// <summary>
/// Smallest Problems surface: status line + list of projected diagnostics.
/// Navigation goes through <see cref="ProblemsViewModel.NavigateToProblemCommand"/>.
/// </summary>
public sealed class ProblemsPanel : ReactiveUserControl<ProblemsViewModel>
{
    private readonly TextBlock _statusText;
    private readonly TextBlock _countText;
    private readonly TextBlock _emptyStateText;
    private readonly ListBox _list;

    public ProblemsPanel()
    {
        ThemeBinding.SetBrush(this, BackgroundProperty, "SurfacePanelBrush");

        var title = TextStyles.Header("Problems");
        _countText = TextStyles.Caption("0");
        _countText.VerticalAlignment = VerticalAlignment.Center;

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingSm,
            Margin = LayoutTokens.Inset(
                LayoutTokens.SpacingMd,
                LayoutTokens.SpacingSm,
                LayoutTokens.SpacingMd,
                LayoutTokens.SpacingXxs),
            Children = { title, _countText },
        };

        _statusText = TextStyles.Caption("Language intelligence unavailable.");
        _statusText.Margin = LayoutTokens.Inset(
            LayoutTokens.SpacingMd, 0, LayoutTokens.SpacingMd, LayoutTokens.SpacingSm);
        _statusText.TextWrapping = TextWrapping.Wrap;

        // F7: Empty state with next-action guidance
        _emptyStateText = new TextBlock
        {
            Text = "No problems detected.\n\nWrite code or build the project to see diagnostics here.",
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = LayoutTokens.Symmetric(LayoutTokens.SpacingLg, LayoutTokens.SpacingLg),
            IsVisible = false,
        };
        ThemeBinding.SetBrush(_emptyStateText, TextBlock.ForegroundProperty, "TextSecondaryBrush");

        _list = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = LayoutTokens.NoneThickness,
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingSm, 0, LayoutTokens.SpacingSm, LayoutTokens.SpacingSm),
        };
        AutomationProperties.SetName(_list, "Problems list");
        AutomationProperties.SetHelpText(
            _list,
            "Diagnostics from the language server and build output. Enter or double-click navigates to the problem.");

        _list.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<ProblemItemViewModel>(
            (item, _) =>
            {
                var text = new TextBlock
                {
                    Text = item.DisplayText,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12,
                };
                ThemeBinding.SetBrush(text, TextBlock.ForegroundProperty, "TextPrimaryBrush");
                return new Border
                {
                    Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingSm, LayoutTokens.SpacingXxs),
                    Child = text,
                };
            });

        _list.DoubleTapped += OnListDoubleTapped;
        _list.KeyDown += OnListKeyDown;

        Content = new DockPanel
        {
            LastChildFill = true,
            Children =
            {
                header,
                _statusText,
                _emptyStateText,
                _list,
            },
        };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(_statusText, Dock.Top);

        this.WhenActivated(d =>
        {
            if (ViewModel is null)
                return;

            d.Add(this.WhenAnyValue(x => x.ViewModel)
                .Where(vm => vm is not null)
                .Subscribe(vm =>
                {
                    _list.ItemsSource = vm!.Problems;
                }));

            d.Add(this.WhenAnyValue(x => x.ViewModel!.StatusMessage)
                .Subscribe(msg =>
                {
                    _statusText.Text = msg ?? string.Empty;
                    _statusText.IsVisible = !string.IsNullOrEmpty(msg);
                    // F7: Also update empty state visibility
                    UpdateEmptyStateVisibility();
                }));

            d.Add(this.WhenAnyValue(x => x.ViewModel!.ProblemCount)
                .Subscribe(count =>
                {
                    _countText.Text = count.ToString();
                    // F7: Show empty state when no problems and no error status
                    UpdateEmptyStateVisibility();
                }));

            d.Add(this.Bind(
                ViewModel,
                vm => vm.SelectedProblem,
                v => v._list.SelectedItem));
        });
    }

    private void OnListDoubleTapped(object? sender, TappedEventArgs e) =>
        NavigateSelected();

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                NavigateSelected();
                e.Handled = true;
                break;
            case Key.Escape:
                if (ViewModel is not null)
                    ViewModel.SelectedProblem = null;
                _list.SelectedItem = null;
                e.Handled = true;
                break;
        }
    }

    private void NavigateSelected()
    {
        if (ViewModel is null)
            return;

        var selected = _list.SelectedItem as ProblemItemViewModel
                       ?? ViewModel.SelectedProblem;
        if (selected is null)
            return;

        ViewModel.NavigateToProblemCommand.Execute(selected).Subscribe();
    }

    private void UpdateEmptyStateVisibility()
    {
        if (ViewModel is null)
            return;

        var hasStatus = !string.IsNullOrEmpty(ViewModel.StatusMessage);
        var hasProblems = ViewModel.ProblemCount > 0;
        var showEmpty = !hasStatus && !hasProblems;

        _emptyStateText.IsVisible = showEmpty;
        _list.IsVisible = !showEmpty;
    }
}
