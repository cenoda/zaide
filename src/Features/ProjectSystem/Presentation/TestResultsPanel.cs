using System;
using System.Collections.Specialized;
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
/// Structured test-results surface distinct from Output and Terminal.
/// </summary>
public sealed class TestResultsPanel : ReactiveUserControl<TestResultsViewModel>
{
    private readonly TextBlock _summaryText;
    private readonly TextBlock _statusText;
    private readonly Button _cancelButton;
    private readonly TextBlock _emptyStateText;
    private readonly ListBox _list;

    public TestResultsPanel()
    {
        ThemeBinding.SetBrush(this, BackgroundProperty, "SurfacePanelBrush");

        var title = TextStyles.Header("Test Results");

        _cancelButton = new Button
        {
            Content = "Cancel",
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingSm, LayoutTokens.SpacingXxs),
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingSm, 0, 0, 0),
            IsVisible = false,
        };

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingSm,
            Margin = LayoutTokens.Inset(
                LayoutTokens.SpacingMd,
                LayoutTokens.SpacingSm,
                LayoutTokens.SpacingMd,
                LayoutTokens.SpacingXxs),
            Children = { title, _cancelButton },
        };

        _summaryText = TextStyles.Caption(string.Empty);
        _summaryText.Margin = LayoutTokens.Inset(
            LayoutTokens.SpacingMd, 0, LayoutTokens.SpacingMd, LayoutTokens.SpacingXxs);
        _summaryText.TextWrapping = TextWrapping.Wrap;

        _statusText = TextStyles.Caption(string.Empty);
        _statusText.Margin = LayoutTokens.Inset(
            LayoutTokens.SpacingMd, 0, LayoutTokens.SpacingMd, LayoutTokens.SpacingSm);
        _statusText.TextWrapping = TextWrapping.Wrap;

        // F7: Empty state with next-action guidance
        _emptyStateText = new TextBlock
        {
            Text = "No test results yet.\n\nRun tests to see results here.",
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = LayoutTokens.Symmetric(LayoutTokens.SpacingLg, LayoutTokens.SpacingLg),
            IsVisible = true,
        };
        ThemeBinding.SetBrush(_emptyStateText, TextBlock.ForegroundProperty, "TextSecondaryBrush");

        _list = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = LayoutTokens.NoneThickness,
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingSm, 0, LayoutTokens.SpacingSm, LayoutTokens.SpacingSm),
        };
        AutomationProperties.SetName(_list, "Test results list");
        AutomationProperties.SetHelpText(
            _list,
            "Structured outcomes from the last dotnet test run. Enter or double-click navigates when a source location is known.");

        _list.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<TestCaseItemViewModel>(
            (item, _) =>
            {
                var text = new TextBlock
                {
                    Text = item!.DisplayText,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12,
                };
                if (item.Result.Outcome == TestCaseOutcome.Failed)
                    text.Foreground = Brushes.OrangeRed;
                else
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
                _summaryText,
                _statusText,
                _emptyStateText,
                _list,
            },
        };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(_summaryText, Dock.Top);
        DockPanel.SetDock(_statusText, Dock.Top);

        this.WhenActivated(d =>
        {
            if (ViewModel is null)
                return;

            d.Add(this.WhenAnyValue(x => x.ViewModel)
                .Where(vm => vm is not null)
                .Subscribe(vm =>
                {
                    _list.ItemsSource = vm!.Cases;
                }));

            d.Add(this.WhenAnyValue(x => x.ViewModel!.SummaryText)
                .Subscribe(text =>
                {
                    _summaryText.Text = text ?? string.Empty;
                    _summaryText.IsVisible = !string.IsNullOrEmpty(text);
                }));

            d.Add(this.WhenAnyValue(x => x.ViewModel!.StatusMessage)
                .Subscribe(msg =>
                {
                    _statusText.Text = msg ?? string.Empty;
                    _statusText.IsVisible = !string.IsNullOrEmpty(msg);
                    // F7: Also update empty state visibility
                    UpdateEmptyStateVisibility();
                }));

            // F7: Update empty state when test cases change
            d.Add(Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                    h => ViewModel!.Cases.CollectionChanged += h,
                    h => ViewModel!.Cases.CollectionChanged -= h)
                .Subscribe(_ => UpdateEmptyStateVisibility()));

            // F7: Initial empty state check
            UpdateEmptyStateVisibility();

            d.Add(this.Bind(
                ViewModel,
                vm => vm.SelectedCase,
                v => v._list.SelectedItem));

            var workflow = ViewModel.Workflow;

            d.Add(workflow.WhenAnyValue(w => w.IsOperationActive)
                .Subscribe(active => _cancelButton.IsVisible = active));

            d.Add(workflow.WhenAnyValue(w => w.CancelAutomationName)
                .Subscribe(name => AutomationProperties.SetName(_cancelButton, name)));

            void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
                workflow.CancelCommand.Execute().Subscribe();
            _cancelButton.Click += OnCancelClick;
            d.Add(Disposable.Create(() => _cancelButton.Click -= OnCancelClick));
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
                    ViewModel.SelectedCase = null;
                _list.SelectedItem = null;
                e.Handled = true;
                break;
        }
    }

    private void NavigateSelected()
    {
        if (ViewModel is null)
            return;

        var selected = _list.SelectedItem as TestCaseItemViewModel
                       ?? ViewModel.SelectedCase;
        if (selected is null || !selected.CanNavigate)
            return;

        ViewModel.NavigateToCaseCommand.Execute(selected).Subscribe();
    }

    private void UpdateEmptyStateVisibility()
    {
        if (ViewModel is null)
            return;

        var hasStatus = !string.IsNullOrEmpty(ViewModel.StatusMessage);
        var hasCases = ViewModel.Cases.Count > 0;
        var showEmpty = !hasStatus && !hasCases;

        _emptyStateText.IsVisible = showEmpty;
        _list.IsVisible = !showEmpty;
    }
}
