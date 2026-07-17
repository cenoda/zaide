using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Zaide.UI.DesignSystem;

namespace Zaide.Views;

/// <summary>
/// Dropdown font picker for Settings. The closed state shows only the selected
/// font; opening reveals a scrollable list of clickable preview rows.
/// </summary>
public sealed class SettingsFontPicker : UserControl
{
    public const double DefaultMaxHeight = 180;

    private readonly Border _trigger;
    private readonly TextBlock _selectedLabel;
    private readonly Popup _popup;
    private readonly ListBox _listBox;
    private readonly Action<string> _onSelected;
    private bool _appliedThisOpen;
    private FontPickerEntry? _confirmedEntry;
    private IReadOnlyList<FontPickerEntry> _entries = Array.Empty<FontPickerEntry>();

    public SettingsFontPicker(Action<string> onSelected, double maxHeight = DefaultMaxHeight)
    {
        _onSelected = onSelected;

        _selectedLabel = new TextBlock
        {
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ResolveBrush("TextPrimaryBrush", "#E3E4F4"),
        };

        _trigger = new Border
        {
            BorderBrush = ResolveBrush("SeparatorBrush", "#070C16"),
            BorderThickness = new Thickness(1),
            CornerRadius = LayoutTokens.RadiusSm,
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingSm, LayoutTokens.SpacingXs),
            Background = ResolveBrush("SurfaceBaseBrush", "#1A2332"),
            Child = _selectedLabel,
            Focusable = true,
        };
        AutomationProperties.SetName(_trigger, "Font family");
        AutomationProperties.SetHelpText(
            _trigger,
            "Click to open the font list. Use Up and Down to browse, Enter to select, Escape to close.");

        _listBox = new ListBox
        {
            MaxHeight = maxHeight,
            Background = Brushes.Transparent,
            BorderThickness = LayoutTokens.NoneThickness,
            SelectionMode = SelectionMode.Single,
        };
        _listBox.Styles.Add(CreateItemStyle());
        _listBox.Styles.Add(CreateSelectedItemStyle());
        _listBox.ItemTemplate = CreateItemTemplate();
        _listBox.KeyDown += OnListKeyDown;
        _listBox.AddHandler(InputElement.PointerPressedEvent, OnListPointerPressed, RoutingStrategies.Tunnel);
        AutomationProperties.SetName(_listBox, "Font list");
        AutomationProperties.SetHelpText(
            _listBox,
            "Use Up and Down to move, Enter or click to accept, Escape to dismiss.");

        var listHost = new Border
        {
            Background = ResolveBrush("SurfaceBaseBrush", "#1A2332"),
            BorderBrush = ResolveBrush("SeparatorBrush", "#070C16"),
            BorderThickness = new Thickness(1),
            CornerRadius = LayoutTokens.RadiusSm,
            ClipToBounds = true,
            Child = _listBox,
        };

        _popup = new Popup
        {
            PlacementTarget = _trigger,
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            IsLightDismissEnabled = true,
            IsHitTestVisible = true,
            Child = listHost,
        };
        _popup.PropertyChanged += OnPopupPropertyChanged;

        _trigger.PointerPressed += OnTriggerPointerPressed;
        _trigger.KeyDown += OnTriggerKeyDown;
        LostFocus += OnLostFocus;

        var host = new Panel();
        host.Children.Add(_trigger);
        host.Children.Add(_popup);
        Content = host;
    }

    /// <summary>Whether the scrollable font list popup is open.</summary>
    public bool IsDropDownOpen => _popup.IsOpen;

    /// <summary>Rebuilds the font list and marks the current family selected.</summary>
    public void SetSelectedFamily(string? familySetting)
    {
        _entries = InstalledFontCatalog.BuildEntries(familySetting);
        _listBox.ItemsSource = _entries;

        var primary = InstalledFontCatalog.ExtractPrimaryFamilyName(familySetting);
        _confirmedEntry = _entries.FirstOrDefault(entry =>
            string.Equals(entry.Name, primary, StringComparison.OrdinalIgnoreCase));

        RestoreListSelection();
        UpdateSelectedLabel(_confirmedEntry);
    }

    private void OnTriggerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_trigger).Properties.IsLeftButtonPressed)
            return;

        ToggleDropDown();
        e.Handled = true;
    }

    private void OnTriggerKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
            case Key.Space:
            case Key.Down:
            case Key.F4:
                if (!_popup.IsOpen)
                    OpenDropDown();
                e.Handled = true;
                break;
            case Key.Escape when _popup.IsOpen:
                CloseDropDown();
                e.Handled = true;
                break;
        }
    }

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                ConfirmSelection();
                e.Handled = true;
                break;
            case Key.Escape:
                CloseDropDown();
                e.Handled = true;
                break;
        }
    }

    private void OnListPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_listBox).Properties.IsLeftButtonPressed)
            return;

        if (TryGetClickedEntry(e.Source, out var entry))
        {
            _listBox.SelectedItem = entry;
            ConfirmSelection();
            e.Handled = true;
        }
    }

    private static bool TryGetClickedEntry(object? source, out FontPickerEntry entry)
    {
        entry = null!;
        if (source is not Visual visual)
            return false;

        if (visual.FindAncestorOfType<ListBoxItem>()?.DataContext is not FontPickerEntry clicked)
            return false;

        entry = clicked;
        return true;
    }

    private void OnPopupPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Popup.IsOpenProperty)
            return;

        if (_popup.IsOpen)
        {
            _appliedThisOpen = false;
            RestoreListSelection();

            if (_confirmedEntry is not null)
                _listBox.ScrollIntoView(_confirmedEntry);

            var width = _trigger.Bounds.Width;
            if (width > 0)
                _popup.MinWidth = width;

            _listBox.Focus(NavigationMethod.Unspecified);
            return;
        }

        if (!_appliedThisOpen)
            RestoreListSelection();
    }

    private void OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (!_popup.IsOpen)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            if (_popup.IsOpen && !IsFocusInsidePicker())
                CloseDropDown();
        }, DispatcherPriority.Input);
    }

    private void ToggleDropDown()
    {
        if (_popup.IsOpen)
            CloseDropDown();
        else
            OpenDropDown();
    }

    private void OpenDropDown()
    {
        if (_popup.IsOpen)
            return;

        _appliedThisOpen = false;
        RestoreListSelection();
        _popup.IsOpen = true;
        _trigger.Focus(NavigationMethod.Unspecified);
    }

    private void CloseDropDown()
    {
        if (!_popup.IsOpen)
            return;

        _popup.IsOpen = false;
        _trigger.Focus(NavigationMethod.Unspecified);
    }

    private void ConfirmSelection()
    {
        if (_listBox.SelectedItem is not FontPickerEntry entry)
            return;

        _confirmedEntry = entry;
        UpdateSelectedLabel(entry);
        _onSelected(entry.Name);
        _appliedThisOpen = true;
        CloseDropDown();
    }

    private void RestoreListSelection() => _listBox.SelectedItem = _confirmedEntry;

    private void UpdateSelectedLabel(FontPickerEntry? entry)
    {
        if (entry is null)
        {
            _selectedLabel.Text = "Select font…";
            _selectedLabel.FontFamily = InstalledFontCatalog.ResolvePreviewFontFamily(string.Empty, false);
            _selectedLabel.Foreground = ResolveBrush("TextSecondaryBrush", "#8B95A5");
            return;
        }

        _selectedLabel.Text = entry.DisplayText;
        _selectedLabel.FontFamily = InstalledFontCatalog.ResolvePreviewFontFamily(
            entry.Name,
            entry.IsAvailable);
        _selectedLabel.Foreground = entry.IsAvailable
            ? ResolveBrush("TextPrimaryBrush", "#E3E4F4")
            : ResolveBrush("TextSecondaryBrush", "#8B95A5");
    }

    private bool IsFocusInsidePicker()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        if (focused is null)
            return false;

        if (ReferenceEquals(focused, _trigger))
            return true;

        if (_popup.Child is Control popupRoot && IsDescendantOf(focused, popupRoot))
            return true;

        return IsDescendantOf(focused, this);
    }

    private static bool IsDescendantOf(Control? node, Control ancestor)
    {
        for (var current = node; current is not null; current = current.Parent as Control)
        {
            if (ReferenceEquals(current, ancestor))
                return true;
        }

        return false;
    }

    private static FuncDataTemplate<FontPickerEntry> CreateItemTemplate()
    {
        return new FuncDataTemplate<FontPickerEntry>((entry, _) =>
        {
            if (entry is null)
                return null;

            var previewFont = InstalledFontCatalog.ResolvePreviewFontFamily(
                entry.Name,
                entry.IsAvailable);

            var label = new TextBlock
            {
                Text = entry.DisplayText,
                FontFamily = previewFont,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = entry.IsAvailable
                    ? ResolveBrush("TextPrimaryBrush", "#E3E4F4")
                    : ResolveBrush("TextSecondaryBrush", "#8B95A5"),
            };

            return new Border
            {
                Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingSm, LayoutTokens.SpacingXs),
                BorderBrush = ResolveBrush("SeparatorBrush", "#070C16"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = label,
            };
        });
    }

    private static Style CreateItemStyle()
    {
        var style = new Style(s => s.OfType<ListBoxItem>());
        style.Setters.Add(new Setter(ListBoxItem.PaddingProperty, LayoutTokens.NoneThickness));
        style.Setters.Add(new Setter(ListBoxItem.MinHeightProperty, 30.0));
        style.Setters.Add(new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        return style;
    }

    private static Style CreateSelectedItemStyle()
    {
        var style = new Style(s => s.OfType<ListBoxItem>().Class(":selected"));
        style.Setters.Add(new Setter(
            ListBoxItem.BackgroundProperty,
            ResolveBrush("SurfaceRaisedBrush", "#243352")));
        style.Setters.Add(new Setter(
            ListBoxItem.BorderBrushProperty,
            ResolveBrush("PrimaryAccentBrush", "#066ADB")));
        style.Setters.Add(new Setter(
            ListBoxItem.BorderThicknessProperty,
            new Thickness(2, 0, 0, 0)));
        return style;
    }

    private static IBrush ResolveBrush(string resourceKey, string fallbackColor)
    {
        if (Application.Current?.Resources.TryGetValue(resourceKey, out var value) == true
            && value is IBrush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Color.Parse(fallbackColor));
    }
}
