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
using Zaide.Services;
using Zaide.Styles;
using Zaide.ViewModels;

namespace Zaide.Views;

/// <summary>
/// Debug bottom panel with Debug Console, Call Stack, and Variables sections.
/// </summary>
public sealed class DebugPanel : ReactiveUserControl<DebugPanelViewModel>
{
    private readonly TextBlock _statusText;
    private readonly ListBox _consoleList;
    private readonly TextBlock _callStackStatus;
    private readonly ListBox _threadList;
    private readonly ListBox _frameList;
    private readonly TextBlock _variablesStatus;
    private readonly ListBox _scopeList;
    private readonly ListBox _variableList;
    private bool _syncingSelection;

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

        _consoleList = CreateReadOnlyList("Debug console lines");
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
            LayoutTokens.SpacingMd, 0, LayoutTokens.SpacingMd, LayoutTokens.SpacingXxs);
        _callStackStatus.TextWrapping = TextWrapping.Wrap;

        _threadList = CreateReadOnlyList("Debug threads");
        _threadList.ItemTemplate = CreateCaptionTemplate<DebugThreadViewModel>(item => item!.DisplayText);
        _threadList.MaxHeight = 72;

        _frameList = CreateReadOnlyList("Call stack frames");
        _frameList.ItemTemplate = CreateCaptionTemplate<DebugStackFrameViewModel>(item => item!.DisplayText);

        var variablesHeader = TextStyles.Header("Variables");
        variablesHeader.Margin = LayoutTokens.Inset(
            LayoutTokens.SpacingMd, LayoutTokens.SpacingSm, LayoutTokens.SpacingMd, LayoutTokens.SpacingXxs);

        _variablesStatus = TextStyles.Caption(string.Empty);
        _variablesStatus.Margin = LayoutTokens.Inset(
            LayoutTokens.SpacingMd, 0, LayoutTokens.SpacingMd, LayoutTokens.SpacingXxs);
        _variablesStatus.TextWrapping = TextWrapping.Wrap;

        _scopeList = CreateReadOnlyList("Debug scopes");
        _scopeList.ItemTemplate = CreateCaptionTemplate<DebugScopeViewModel>(item => item!.Name);
        _scopeList.MaxHeight = 72;

        _variableList = CreateReadOnlyList("Debug variables");
        _variableList.ItemTemplate = CreateCaptionTemplate<DebugVariableViewModel>(item => item!.DisplayText);

        var consoleSection = new DockPanel
        {
            LastChildFill = true,
            Children = { consoleHeader, _consoleList },
        };
        DockPanel.SetDock(consoleHeader, Dock.Top);

        var callStackContent = new DockPanel
        {
            LastChildFill = true,
            Children = { _callStackStatus, _threadList, _frameList },
        };
        DockPanel.SetDock(_callStackStatus, Dock.Top);
        DockPanel.SetDock(_threadList, Dock.Top);

        var callStackSection = new DockPanel
        {
            LastChildFill = true,
            Children = { callStackHeader, callStackContent },
        };
        DockPanel.SetDock(callStackHeader, Dock.Top);

        var variablesContent = new DockPanel
        {
            LastChildFill = true,
            Children = { _variablesStatus, _scopeList, _variableList },
        };
        DockPanel.SetDock(_variablesStatus, Dock.Top);
        DockPanel.SetDock(_scopeList, Dock.Top);

        var variablesSection = new DockPanel
        {
            LastChildFill = true,
            Children = { variablesHeader, variablesContent },
        };
        DockPanel.SetDock(variablesHeader, Dock.Top);

        var sections = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(4, GridUnitType.Pixel) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(4, GridUnitType.Pixel) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
            Children = { consoleSection, callStackSection, variablesSection },
        };
        Grid.SetColumn(consoleSection, 0);
        Grid.SetColumn(callStackSection, 2);
        Grid.SetColumn(variablesSection, 4);

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

            BindStackProjection(d);
        });
    }

    private void BindStackProjection(CompositeDisposable d)
    {
        d.Add(this.WhenAnyValue(x => x.ViewModel!.StackProjection.Threads)
            .Subscribe(threads => _threadList.ItemsSource = threads));

        d.Add(this.WhenAnyValue(x => x.ViewModel!.StackProjection.Frames)
            .Subscribe(frames => _frameList.ItemsSource = frames));

        d.Add(this.WhenAnyValue(x => x.ViewModel!.StackProjection.Scopes)
            .Subscribe(scopes => _scopeList.ItemsSource = scopes));

        d.Add(this.WhenAnyValue(x => x.ViewModel!.StackProjection.Variables)
            .Subscribe(variables => _variableList.ItemsSource = variables));

        d.Add(this.WhenAnyValue(x => x.ViewModel!.StackProjection.CallStackStatusText)
            .Subscribe(text =>
            {
                _callStackStatus.Text = text;
                _callStackStatus.IsVisible = !string.IsNullOrEmpty(text);
            }));

        d.Add(this.WhenAnyValue(x => x.ViewModel!.StackProjection.VariablesStatusText)
            .Subscribe(text =>
            {
                _variablesStatus.Text = text;
                _variablesStatus.IsVisible = !string.IsNullOrEmpty(text);
            }));

        d.Add(this.WhenAnyValue(x => x.ViewModel!.StackProjection.CallStackState)
            .Subscribe(state =>
            {
                _threadList.IsVisible = state == DebugProjectionState.Ready &&
                                        ViewModel!.StackProjection.Threads.Count > 1;
                _frameList.IsVisible = state == DebugProjectionState.Ready;
            }));

        d.Add(this.WhenAnyValue(x => x.ViewModel!.StackProjection.VariablesState)
            .Subscribe(state =>
            {
                _scopeList.IsVisible = state == DebugProjectionState.Ready &&
                                       ViewModel!.StackProjection.Scopes.Count > 1;
                _variableList.IsVisible = state == DebugProjectionState.Ready;
            }));

        d.Add(Observable.FromEventPattern<EventHandler<SelectionChangedEventArgs>, SelectionChangedEventArgs>(
                h => _threadList.SelectionChanged += h,
                h => _threadList.SelectionChanged -= h)
            .Subscribe(_ =>
            {
                if (_syncingSelection ||
                    ViewModel is null ||
                    _threadList.SelectedItem is not DebugThreadViewModel thread)
                    return;

                ViewModel.StackProjection.SelectThreadCommand.Execute(thread).Subscribe();
            }));

        d.Add(Observable.FromEventPattern<EventHandler<SelectionChangedEventArgs>, SelectionChangedEventArgs>(
                h => _frameList.SelectionChanged += h,
                h => _frameList.SelectionChanged -= h)
            .Subscribe(_ =>
            {
                if (_syncingSelection ||
                    ViewModel is null ||
                    _frameList.SelectedItem is not DebugStackFrameViewModel frame)
                    return;

                ViewModel.StackProjection.SelectFrameCommand.Execute(frame).Subscribe();
            }));

        d.Add(Observable.FromEventPattern<EventHandler<SelectionChangedEventArgs>, SelectionChangedEventArgs>(
                h => _scopeList.SelectionChanged += h,
                h => _scopeList.SelectionChanged -= h)
            .Subscribe(_ =>
            {
                if (_syncingSelection ||
                    ViewModel is null ||
                    _scopeList.SelectedItem is not DebugScopeViewModel scope)
                    return;

                ViewModel.StackProjection.SelectScopeCommand.Execute(scope).Subscribe();
            }));

        d.Add(this.WhenAnyValue(x => x.ViewModel!.StackProjection.SelectedThread)
            .Subscribe(thread => SyncListSelection(_threadList, thread)));

        d.Add(this.WhenAnyValue(x => x.ViewModel!.StackProjection.SelectedFrame)
            .Subscribe(frame => SyncListSelection(_frameList, frame)));

        d.Add(this.WhenAnyValue(x => x.ViewModel!.StackProjection.SelectedScope)
            .Subscribe(scope => SyncListSelection(_scopeList, scope)));
    }

    private void SyncListSelection(ListBox list, object? item)
    {
        if (item is null)
        {
            if (list.SelectedItem is null)
                return;

            _syncingSelection = true;
            try
            {
                list.SelectedItem = null;
            }
            finally
            {
                _syncingSelection = false;
            }

            return;
        }

        if (ReferenceEquals(list.SelectedItem, item))
            return;

        _syncingSelection = true;
        try
        {
            list.SelectedItem = item;
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private static ListBox CreateReadOnlyList(string automationName)
    {
        var list = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = LayoutTokens.NoneThickness,
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingSm, 0, LayoutTokens.SpacingSm, LayoutTokens.SpacingSm),
        };
        AutomationProperties.SetName(list, automationName);
        AutomationProperties.SetHelpText(list, "Read-only debug projection list.");
        return list;
    }

    private static Avalonia.Controls.Templates.FuncDataTemplate<T> CreateCaptionTemplate<T>(
        Func<T?, string> getText)
    {
        return new Avalonia.Controls.Templates.FuncDataTemplate<T>((item, _) =>
            new Border
            {
                Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingSm, LayoutTokens.SpacingXxs),
                Child = TextStyles.Caption(getText(item)),
            });
    }
}