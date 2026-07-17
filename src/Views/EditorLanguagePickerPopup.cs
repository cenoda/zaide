using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Zaide.UI.DesignSystem;

namespace Zaide.Views;

/// <summary>
/// Minimal accessible list popup for definition multi-result chooser and symbol surfaces.
/// Presentation-only: binds display strings provided by the host.
/// </summary>
public sealed class EditorLanguagePickerPopup : Popup
{
    private readonly ListBox _listBox;
    private readonly TextBlock _header;
    private readonly TextBox? _queryBox;
    private readonly bool _showQuery;

    public EditorLanguagePickerPopup(bool showQuery = false)
    {
        _showQuery = showQuery;
        IsLightDismissEnabled = false;
        Placement = PlacementMode.Bottom;
        IsHitTestVisible = true;

        _header = new TextBlock
        {
            FontSize = 11,
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
            Margin = new Thickness(LayoutTokens.SpacingXs, LayoutTokens.SpacingXxs),
        };

        _listBox = new ListBox
        {
            MinWidth = 280,
            MaxHeight = 260,
            SelectionMode = SelectionMode.Single,
        };
        _listBox.KeyDown += OnListKeyDown;

        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = LayoutTokens.SpacingXxs,
        };
        panel.Children.Add(_header);

        if (_showQuery)
        {
            _queryBox = new TextBox
            {
                PlaceholderText = "Filter workspace symbols…",
                MinWidth = 280,
                Margin = new Thickness(LayoutTokens.SpacingXxs),
            };
            AutomationProperties.SetName(_queryBox, "Workspace symbol filter");
            AutomationProperties.SetHelpText(
                _queryBox,
                "Type to filter workspace symbols. Escape dismisses the picker.");
            _queryBox.TextChanged += (_, _) => QueryChanged?.Invoke(_queryBox.Text ?? string.Empty);
            _queryBox.KeyDown += OnQueryKeyDown;
            panel.Children.Add(_queryBox);
        }

        panel.Children.Add(_listBox);

        var border = new Border
        {
            Background = (IBrush?)Application.Current!.Resources["SurfacePanelBrush"],
            BorderBrush = (IBrush?)Application.Current!.Resources["BorderSubtleBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = LayoutTokens.RadiusSm,
            Padding = new Thickness(LayoutTokens.SpacingXxs),
            Child = panel,
        };

        Child = border;
    }

    /// <summary>Raised when the user confirms a list selection.</summary>
    public event Action? ItemConfirmed;

    /// <summary>Raised when the user dismisses the popup with Escape.</summary>
    public event Action? DismissRequested;

    /// <summary>Raised when the workspace query text changes (query-enabled pickers only).</summary>
    public event Action<string>? QueryChanged;

    public void SetHeader(string? text)
    {
        _header.Text = text ?? string.Empty;
        AutomationProperties.SetName(_listBox, text ?? "Symbol picker");
        AutomationProperties.SetHelpText(
            _listBox,
            "Use Up and Down to move, Enter to accept, Escape to dismiss.");
    }

    public void BindItems(IReadOnlyList<string> labels, int selectedIndex)
    {
        _listBox.ItemsSource = labels;

        if (labels.Count == 0)
            return;

        var clamped = Math.Clamp(selectedIndex, 0, labels.Count - 1);
        _listBox.SelectedIndex = clamped;
        _listBox.ScrollIntoView(labels[clamped]);
    }

    public void ClearQuery()
    {
        if (_queryBox is not null)
            _queryBox.Text = string.Empty;
    }

    public void FocusQuery()
    {
        if (_queryBox is not null)
            _queryBox.Focus(NavigationMethod.Unspecified);
        else
            _listBox.Focus(NavigationMethod.Unspecified);
    }

    public void ConfirmSelection() => ItemConfirmed?.Invoke();

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                DismissRequested?.Invoke();
                e.Handled = true;
                break;
            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Down:
                MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Enter:
                ConfirmSelection();
                e.Handled = true;
                break;
        }
    }

    private void OnQueryKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DismissRequested?.Invoke();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Enter or Key.Down && _listBox.ItemCount > 0)
        {
            _listBox.Focus(NavigationMethod.Unspecified);
            e.Handled = true;
        }
    }

    private void MoveSelection(int delta)
    {
        if (_listBox.ItemCount == 0)
            return;

        var next = _listBox.SelectedIndex + delta;
        if (next < 0)
            next = _listBox.ItemCount - 1;
        else if (next >= _listBox.ItemCount)
            next = 0;

        _listBox.SelectedIndex = next;
        if (_listBox.SelectedItem is not null)
            _listBox.ScrollIntoView(_listBox.SelectedItem);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsOpenProperty && IsOpen)
            FocusQuery();
    }
}