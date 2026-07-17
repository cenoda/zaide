using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Zaide.App.Shell;

namespace Zaide.Features.Editor.Presentation;

/// <summary>
/// Focused search/replace bar for the active editor document.
/// Built entirely in C# to match the project's code-first UI convention.
/// Binds to <see cref="EditorSearchViewModel"/> for all state and commands.
/// </summary>
public sealed class SearchBar : UserControl
{
    private readonly TextBox _queryBox;
    private readonly TextBox _replaceBox;
    private readonly TextBlock _matchInfo;
    private readonly Button _caseToggle;
    private readonly StackPanel _replacePanel;

    public SearchBar(EditorSearchViewModel searchViewModel)
    {
        DataContext = searchViewModel;

        // ── Query row ────────────────────────────────────────────────────
        _queryBox = new TextBox
        {
            Width = 200,
            PlaceholderText = "Find"
        };
        DockPanel.SetDock(_queryBox, Dock.Left);
        _queryBox.KeyDown += OnQueryKeyDown;

        var prevBtn = new Button
        {
            Content = "▲",
            Width = 28,
            Padding = new Thickness(0)
        };
        DockPanel.SetDock(prevBtn, Dock.Left);
        ToolTip.SetTip(prevBtn, "Find Previous (Shift+F3)");
        prevBtn.Click += (_, _) => searchViewModel.FindPreviousCommand.Execute(null);

        var nextBtn = new Button
        {
            Content = "▼",
            Width = 28,
            Padding = new Thickness(0)
        };
        DockPanel.SetDock(nextBtn, Dock.Left);
        ToolTip.SetTip(nextBtn, "Find Next (F3)");
        nextBtn.Click += (_, _) => searchViewModel.FindNextCommand.Execute(null);

        _matchInfo = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0)
        };
        DockPanel.SetDock(_matchInfo, Dock.Left);
        _matchInfo.Bind(TextBlock.TextProperty, new MultiBinding
        {
            Converter = new MatchCountConverter(),
            Bindings =
            {
                new Binding("CurrentMatchIndex"),
                new Binding("MatchCount"),
                new Binding("StatusMessage")
            }
        });

        _caseToggle = new Button
        {
            Content = "Aa",
            Width = 28,
            Padding = new Thickness(0)
        };
        DockPanel.SetDock(_caseToggle, Dock.Right);
        ToolTip.SetTip(_caseToggle, "Toggle Case Sensitivity");
        _caseToggle.Click += (_, _) => searchViewModel.CaseSensitive = !searchViewModel.CaseSensitive;
        _caseToggle.Bind(BackgroundProperty, new Binding("CaseSensitive")
        {
            Converter = new CaseToggleBrushConverter()
        });

        var closeBtn = new Button
        {
            Content = "✕",
            Width = 28,
            Padding = new Thickness(0)
        };
        DockPanel.SetDock(closeBtn, Dock.Right);
        ToolTip.SetTip(closeBtn, "Close (Esc)");
        closeBtn.Click += (_, _) => searchViewModel.Dismiss();

        var queryRow = new DockPanel
        {
            Margin = new Thickness(4, 2, 4, 2),
            Children = { closeBtn, _caseToggle, _queryBox, prevBtn, nextBtn, _matchInfo }
        };

        // ── Replace row ──────────────────────────────────────────────────
        _replaceBox = new TextBox
        {
            Width = 200,
            PlaceholderText = "Replace"
        };
        DockPanel.SetDock(_replaceBox, Dock.Left);
        _replaceBox.KeyDown += OnReplaceKeyDown;

        var replaceOneBtn = new Button
        {
            Content = "Replace",
            Padding = new Thickness(6, 0)
        };
        DockPanel.SetDock(replaceOneBtn, Dock.Left);
        ToolTip.SetTip(replaceOneBtn, "Replace Next");
        replaceOneBtn.Click += (_, _) => searchViewModel.ReplaceNextCommand.Execute(null);

        var replaceAllBtn = new Button
        {
            Content = "All",
            Padding = new Thickness(6, 0)
        };
        DockPanel.SetDock(replaceAllBtn, Dock.Left);
        ToolTip.SetTip(replaceAllBtn, "Replace All");
        replaceAllBtn.Click += (_, _) => searchViewModel.ReplaceAllCommand.Execute(null);

        _replacePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(4, 0, 4, 2),
            Children = { _replaceBox, replaceOneBtn, replaceAllBtn }
        };
        _replacePanel.Bind(IsVisibleProperty, new Binding("IsReplaceMode"));

        // ── Layout ───────────────────────────────────────────────────────
        Content = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Children = { queryRow, _replacePanel }
        };

        // ── Two-way bindings ─────────────────────────────────────────────
        _queryBox.Bind(TextBox.TextProperty, new Binding("Query") { Mode = BindingMode.TwoWay });
        _replaceBox.Bind(TextBox.TextProperty, new Binding("ReplacementText") { Mode = BindingMode.TwoWay });

        // Visibility is driven by the ViewModel's IsVisible property.
        // FindCommand/ReplaceCommand set IsVisible = true; Dismiss sets it false.
        // Set initial value explicitly.
        IsVisible = false;
        
        // Subscribe to ViewModel property changes for visibility.
        // This is more reliable than data binding in test environments.
        searchViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(EditorSearchViewModel.IsVisible))
            {
                IsVisible = searchViewModel.IsVisible;
            }
        };
    }

    /// <summary>
    /// Focuses the query text box and selects all text.
    /// Called by MainWindow when the search surface opens.
    /// </summary>
    public void FocusQuery()
    {
        _queryBox.Focus();
        _queryBox.SelectAll();
    }

    /// <summary>
    /// Focuses the query text box without selecting all text.
    /// Called after search navigation (FindNext/Previous) to ensure the
    /// search bar retains focus without disrupting the user's query text.
    /// </summary>
    public void FocusQueryWithoutSelectAll()
    {
        _queryBox.Focus();
    }

    private void OnQueryKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Control control) return;
        var vm = control.DataContext as EditorSearchViewModel;
        if (vm is null) return;

        switch (e.Key)
        {
            case Key.Enter:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    vm.FindPreviousCommand.Execute(null);
                else
                    vm.FindNextCommand.Execute(null);
                // Restore focus to query box after navigation; SelectCurrentMatch
                // may steal editor focus on Linux (X11 input-focus redirection).
                _queryBox.Focus();
                e.Handled = true;
                break;
            case Key.Escape:
                vm.Dismiss();
                e.Handled = true;
                break;
        }
    }

    private void OnReplaceKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Control control) return;
        var vm = control.DataContext as EditorSearchViewModel;
        if (vm is null) return;

        switch (e.Key)
        {
            case Key.Enter:
                vm.ReplaceNextCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                vm.Dismiss();
                e.Handled = true;
                break;
        }
    }

    // ── Converters ───────────────────────────────────────────────────────

    /// <summary>
    /// Converts (CurrentMatchIndex, MatchCount, StatusMessage) into a display string.
    /// Shows "X of Y" when matches exist, "No matches" when zero, empty otherwise.
    /// </summary>
    private sealed class MatchCountConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 3) return string.Empty;
            if (values[1] is int count && count > 0 && values[0] is int index)
                return $"{index + 1} of {count}";
            if (values[2] is string status && status == "No matches found")
                return "No matches";
            return string.Empty;
        }
    }

    /// <summary>
    /// Returns a highlighted brush when case-sensitivity is on, transparent otherwise.
    /// </summary>
    private sealed class CaseToggleBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is true)
                return new SolidColorBrush(Color.FromArgb(60, 100, 149, 237));
            return Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
