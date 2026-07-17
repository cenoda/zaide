using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Zaide.Services;
using Zaide.UI.DesignSystem;
using Zaide.Features.Language.Application;

namespace Zaide.Features.Editor.Presentation;

/// <summary>
/// Minimal accessible completion popup for the shared editor.
/// </summary>
public sealed class EditorCompletionPopup : Popup
{
    private readonly ListBox _listBox;

    public EditorCompletionPopup()
    {
        IsLightDismissEnabled = false;
        Placement = PlacementMode.Bottom;
        IsHitTestVisible = true;

        _listBox = new ListBox
        {
            MinWidth = 220,
            MaxHeight = 220,
            SelectionMode = SelectionMode.Single,
        };
        AutomationProperties.SetName(_listBox, "Completion suggestions");
        AutomationProperties.SetHelpText(
            _listBox,
            "Use Up and Down to move, Enter or Tab to accept, Escape to dismiss.");
        _listBox.KeyDown += OnListKeyDown;

        var border = new Border
        {
            Background = (IBrush?)Application.Current!.Resources["SurfacePanelBrush"],
            BorderBrush = (IBrush?)Application.Current!.Resources["BorderSubtleBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = LayoutTokens.RadiusSm,
            Padding = new Thickness(LayoutTokens.SpacingXxs),
            Child = _listBox,
        };
        AutomationProperties.SetName(border, "Completion popup");

        Child = border;
    }

    /// <summary>Raised when the user confirms a list selection with pointer/keyboard.</summary>
    public event Action? ItemConfirmed;

    /// <summary>Raised when the user dismisses the popup with Escape.</summary>
    public event Action? DismissRequested;

    public void BindItems(IReadOnlyList<LanguageCompletionItem> items, int selectedIndex)
    {
        _listBox.ItemsSource = items;
        _listBox.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(LanguageCompletionItem.Label));

        if (items.Count == 0)
            return;

        var clamped = Math.Clamp(selectedIndex, 0, items.Count - 1);
        _listBox.SelectedIndex = clamped;
        _listBox.ScrollIntoView(items[clamped]);
    }

    public void MoveSelection(int delta)
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
            case Key.Tab:
                ConfirmSelection();
                e.Handled = true;
                break;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsOpenProperty && IsOpen)
            _listBox.Focus(NavigationMethod.Unspecified);
    }
}