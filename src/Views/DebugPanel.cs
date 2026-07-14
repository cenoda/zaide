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
/// Debug bottom panel with separate Debug Console and Call Stack sections.
/// </summary>
public sealed class DebugPanel : ReactiveUserControl<DebugPanelViewModel>
{
    private readonly TextBlock _statusText;
    private readonly ListBox _consoleList;
    private readonly TextBlock _callStackStatus;

    public DebugPanel()
    {
        Background = (IBrush?)Application.Current?.Resources["SurfacePanelBrush"]
            ?? Brushes.Transparent;

        _statusText = TextStyles.Caption(string.Empty);
        _statusText.Margin = LayoutTokens.Inset(
            LayoutTokens.SpacingMd, 0, LayoutTokens.SpacingMd, LayoutTokens.SpacingSm);
        _statusText.TextWrapping = TextWrapping.Wrap;

        var consoleHeader = TextStyles.Header("Debug Console");
        consoleHeader.Margin = LayoutTokens.Inset(
            LayoutTokens.SpacingMd, LayoutTokens.SpacingSm, LayoutTokens.SpacingMd, LayoutTokens.SpacingXxs);

        _consoleList = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = LayoutTokens.NoneThickness,
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingSm, 0, LayoutTokens.SpacingSm, LayoutTokens.SpacingSm),
        };
        AutomationProperties.SetName(_consoleList, "Debug console lines");
        AutomationProperties.SetHelpText(
            _consoleList,
            "Structured debug-session output distinct from terminal and workflow output. Read-only.");

        _consoleList.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<DebugConsoleLineViewModel>(
            (item, _) =>
            {
                var brushKey = item!.Kind switch
                {
                    DebugConsoleLineKind.Error => "WarningBrush",
                    _ => "TextPrimaryBrush",
                };

                var text = new TextBlock
                {
                    Text = item.DisplayText,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new FontFamily("monospace"),
                    FontSize = 12,
                    Foreground = (IBrush?)Application.Current?.Resources[brushKey]
                        ?? Brushes.White,
                };
                return new Border
                {
                    Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingSm, LayoutTokens.SpacingXxs),
                    Child = text,
                };
            });

        var callStackHeader = TextStyles.Header("Call Stack");
        callStackHeader.Margin = LayoutTokens.Inset(
            LayoutTokens.SpacingMd, LayoutTokens.SpacingSm, LayoutTokens.SpacingMd, LayoutTokens.SpacingXxs);

        _callStackStatus = TextStyles.Caption(string.Empty);
        _callStackStatus.Margin = LayoutTokens.Inset(
            LayoutTokens.SpacingMd, 0, LayoutTokens.SpacingMd, LayoutTokens.SpacingMd);
        _callStackStatus.TextWrapping = TextWrapping.Wrap;
        AutomationProperties.SetName(_callStackStatus, "Call stack status");

        var consoleSection = new DockPanel
        {
            LastChildFill = true,
            Children = { consoleHeader, _consoleList },
        };
        DockPanel.SetDock(consoleHeader, Dock.Top);

        var callStackSection = new DockPanel
        {
            LastChildFill = true,
            Children = { callStackHeader, _callStackStatus },
        };
        DockPanel.SetDock(callStackHeader, Dock.Top);

        var sections = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(4, GridUnitType.Pixel) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
            Children = { consoleSection, callStackSection },
        };
        Grid.SetColumn(consoleSection, 0);
        Grid.SetColumn(callStackSection, 2);

        Content = new DockPanel
        {
            LastChildFill = true,
            Children = { _statusText, sections },
        };
        DockPanel.SetDock(_statusText, Dock.Top);

        this.WhenActivated(d =>
        {
            if (ViewModel is null)
                return;

            d.Add(this.WhenAnyValue(x => x.ViewModel)
                .Where(vm => vm is not null)
                .Subscribe(vm =>
                {
                    _consoleList.ItemsSource = vm!.Lines;
                }));

            d.Add(Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                    h => ViewModel!.Lines.CollectionChanged += h,
                    h => ViewModel!.Lines.CollectionChanged -= h)
                .Subscribe(e =>
                {
                    if (e.EventArgs.Action != NotifyCollectionChangedAction.Add ||
                        e.EventArgs.NewItems is not { Count: > 0 })
                        return;

                    var scrollViewer = _consoleList.FindDescendantOfType<ScrollViewer>();
                    if (scrollViewer is null)
                        return;

                    const double threshold = 20.0;
                    var maxOffset = scrollViewer.ScrollBarMaximum.Y;
                    if (scrollViewer.Offset.Y >= maxOffset - threshold)
                        _consoleList.ScrollIntoView(e.EventArgs.NewItems[^1]!);
                }));

            d.Add(this.WhenAnyValue(x => x.ViewModel!.StatusMessage)
                .Subscribe(msg =>
                {
                    _statusText.Text = msg ?? string.Empty;
                    _statusText.IsVisible = !string.IsNullOrEmpty(msg);
                }));

            d.Add(this.WhenAnyValue(x => x.ViewModel!.CallStackStatusText)
                .Subscribe(text => _callStackStatus.Text = text));
        });
    }
}