using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Zaide.Styles;
using Zaide.ViewModels;

namespace Zaide.Views;

/// <summary>
/// Smallest Problems surface: status line + list of projected diagnostics.
/// Navigation goes through <see cref="ProblemsViewModel.NavigateToProblemCommand"/>.
/// </summary>
public sealed class ProblemsPanel : ReactiveUserControl<ProblemsViewModel>
{
    private readonly TextBlock _statusText;
    private readonly TextBlock _countText;
    private readonly ListBox _list;

    public ProblemsPanel()
    {
        Background = (IBrush?)Application.Current!.Resources["SurfacePanelBrush"];

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

        _list = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = LayoutTokens.NoneThickness,
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingSm, 0, LayoutTokens.SpacingSm, LayoutTokens.SpacingSm),
        };

        _list.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<ProblemItemViewModel>(
            (item, _) =>
            {
                var text = new TextBlock
                {
                    Text = item.DisplayText,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12,
                    Foreground = (IBrush?)Application.Current!.Resources["TextPrimaryBrush"],
                };
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
                }));

            d.Add(this.WhenAnyValue(x => x.ViewModel!.ProblemCount)
                .Subscribe(count => _countText.Text = count.ToString()));

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
        if (e.Key is Key.Enter or Key.Return)
        {
            NavigateSelected();
            e.Handled = true;
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
}
