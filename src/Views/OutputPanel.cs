using System;
using System.Collections.Specialized;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Zaide.Styles;
using Zaide.ViewModels;

namespace Zaide.Views;

/// <summary>
/// Read-only structured process output surface distinct from the interactive terminal.
/// </summary>
public sealed class OutputPanel : ReactiveUserControl<ProjectWorkflowViewModel>
{
    private readonly TextBlock _statusText;
    private readonly Button _cancelButton;
    private readonly ListBox _list;

    public OutputPanel()
    {
        Background = (IBrush?)Application.Current?.Resources["SurfacePanelBrush"]
            ?? Brushes.Transparent;

        var title = TextStyles.Header("Output");

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

        _statusText = TextStyles.Caption(string.Empty);
        _statusText.Margin = LayoutTokens.Inset(
            LayoutTokens.SpacingMd, 0, LayoutTokens.SpacingMd, LayoutTokens.SpacingSm);
        _statusText.TextWrapping = TextWrapping.Wrap;

        _list = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = LayoutTokens.NoneThickness,
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingSm, 0, LayoutTokens.SpacingSm, LayoutTokens.SpacingSm),
        };
        AutomationProperties.SetName(_list, "Output lines");
        AutomationProperties.SetHelpText(
            _list,
            "Structured stdout and stderr from build, run, and test operations. Read-only.");

        _list.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<OutputLineViewModel>(
            (item, _) =>
            {
                var text = new TextBlock
                {
                    Text = item!.DisplayText,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new FontFamily("monospace"),
                    FontSize = 12,
                    Foreground = item.StreamTag == "stderr"
                        ? Brushes.OrangeRed
                        : (IBrush?)Application.Current?.Resources["TextPrimaryBrush"]
                            ?? Brushes.White,
                };
                return new Border
                {
                    Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingSm, LayoutTokens.SpacingXxs),
                    Child = text,
                };
            });

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
                    _list.ItemsSource = vm!.Lines;
                }));

            // Scroll-follow: auto-scroll to the latest line only when the user
            // is already near the bottom. When the user has scrolled upward,
            // new output does not forcibly jump the viewport.
            d.Add(Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                    h => ViewModel!.Lines.CollectionChanged += h,
                    h => ViewModel!.Lines.CollectionChanged -= h)
                .Subscribe(e =>
                {
                    if (e.EventArgs.Action != NotifyCollectionChangedAction.Add ||
                        e.EventArgs.NewItems is not { Count: > 0 })
                        return;

                    var scrollViewer = _list.FindDescendantOfType<ScrollViewer>();
                    if (scrollViewer is null)
                        return;

                    // "Near bottom" threshold: within 20px of the maximum
                    // scroll offset. This prevents a jump when the user has
                    // scrolled up to read earlier output.
                    const double threshold = 20.0;
                    var maxOffset = scrollViewer.ScrollBarMaximum.Y;
                    if (scrollViewer.Offset.Y >= maxOffset - threshold)
                    {
                        _list.ScrollIntoView(e.EventArgs.NewItems[^1]!);
                    }
                }));

            d.Add(this.WhenAnyValue(x => x.ViewModel!.StatusMessage)
                .Subscribe(msg =>
                {
                    _statusText.Text = msg ?? string.Empty;
                    _statusText.IsVisible = !string.IsNullOrEmpty(msg);
                }));

            d.Add(this.WhenAnyValue(x => x.ViewModel!.IsOperationActive)
                .Subscribe(active => _cancelButton.IsVisible = active));

            d.Add(this.WhenAnyValue(x => x.ViewModel!.CancelAutomationName)
                .Subscribe(name => AutomationProperties.SetName(_cancelButton, name)));

            void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
                ViewModel!.CancelCommand.Execute().Subscribe();
            _cancelButton.Click += OnCancelClick;
            d.Add(Disposable.Create(() => _cancelButton.Click -= OnCancelClick));
        });
    }
}
