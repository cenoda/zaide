using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using ReactiveUI.Avalonia;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Views;

namespace Zaide.Tests.Views;

public sealed class SettingsFontPickerTests
{
    static SettingsFontPickerTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
        EnsureApplication();
    }

    [Fact]
    public void Picker_StartsClosed_ShowingOnlySelectedFont()
    {
        var entries = InstalledFontCatalog.BuildEntries(null);
        var target = entries.First(entry => entry.IsAvailable);

        var picker = new SettingsFontPicker(_ => { });
        picker.SetSelectedFamily(target.Name);

        Assert.False(picker.IsDropDownOpen);
        var selectedLabel = GetAllDescendants(picker)
            .OfType<TextBlock>()
            .Single(tb => tb.Parent is Border);
        Assert.Equal(target.DisplayText, selectedLabel.Text);
    }

    [Fact]
    public void Picker_UsesBoundedScrollableListBox()
    {
        var picker = new SettingsFontPicker(_ => { });
        picker.SetSelectedFamily(null);

        var listBox = GetAllDescendants(picker).OfType<ListBox>().Single();
        Assert.Equal(SettingsFontPicker.DefaultMaxHeight, listBox.MaxHeight);
        Assert.True(listBox.MaxHeight > 0);
    }

    [Fact]
    public void OpenDropDown_RevealsScrollableFontList()
    {
        var picker = new SettingsFontPicker(_ => { });
        picker.SetSelectedFamily(null);

        var popup = GetAllDescendants(picker).OfType<Popup>().Single();
        popup.IsOpen = true;

        Assert.True(picker.IsDropDownOpen);
        var listBox = GetAllDescendants(picker).OfType<ListBox>().Single();
        var items = listBox.ItemsSource as System.Collections.IEnumerable;
        Assert.NotNull(items);
        Assert.True(items!.Cast<object>().Count() > 8);
    }

    [Fact]
    public void SetSelectedFamily_MarksMatchingEntrySelected()
    {
        var entries = InstalledFontCatalog.BuildEntries(null);
        var target = entries.First(entry => entry.IsAvailable);

        var picker = new SettingsFontPicker(_ => { });
        picker.SetSelectedFamily(target.Name);

        var listBox = GetAllDescendants(picker).OfType<ListBox>().Single();
        var selected = Assert.IsType<FontPickerEntry>(listBox.SelectedItem);
        Assert.Equal(target.Name, selected.Name);
    }

    [Fact]
    public void ConfirmSelection_InvokesCallbackWithFamilyName()
    {
        var entries = InstalledFontCatalog.BuildEntries(null);
        var target = entries.First(entry => entry.IsAvailable);
        string? selected = null;

        var picker = new SettingsFontPicker(name => selected = name);
        picker.SetSelectedFamily(entries.First(entry => !entry.Name.Equals(target.Name, System.StringComparison.OrdinalIgnoreCase)).Name);

        var listBox = GetAllDescendants(picker).OfType<ListBox>().Single();
        listBox.SelectedItem = target;
        listBox.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Source = listBox,
            Key = Key.Enter,
        });

        Assert.Equal(target.Name, selected);
        Assert.False(picker.IsDropDownOpen);
    }

    [Fact]
    public void Escape_ClosesDropDown_WithoutChangingSelection()
    {
        var entries = InstalledFontCatalog.BuildEntries(null);
        var original = entries.First(entry => entry.IsAvailable);
        var alternate = entries.First(entry =>
            entry.IsAvailable
            && !entry.Name.Equals(original.Name, System.StringComparison.OrdinalIgnoreCase));
        string? selected = original.Name;

        var picker = new SettingsFontPicker(name => selected = name);
        picker.SetSelectedFamily(original.Name);

        var popup = GetAllDescendants(picker).OfType<Popup>().Single();
        popup.IsOpen = true;

        var listBox = GetAllDescendants(picker).OfType<ListBox>().Single();
        listBox.SelectedItem = alternate;
        listBox.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Source = listBox,
            Key = Key.Escape,
        });

        Assert.False(picker.IsDropDownOpen);
        Assert.Equal(original.Name, selected);
        var current = Assert.IsType<FontPickerEntry>(listBox.SelectedItem);
        Assert.Equal(original.Name, current.Name);
    }

    [Fact]
    public void BuildEntries_ExceedsPickerHeight_ForSmoothScrolling()
    {
        var entries = InstalledFontCatalog.BuildEntries(null);
        Assert.True(entries.Count > 8, "Expected enough installed fonts to require scrolling.");
    }

    private static System.Collections.Generic.IEnumerable<Control> GetAllDescendants(Control parent)
    {
        foreach (var child in VisualChildren(parent))
        {
            yield return child;
            foreach (var descendant in GetAllDescendants(child))
                yield return descendant;
        }
    }

    private static System.Collections.Generic.IEnumerable<Control> VisualChildren(Control parent)
    {
        if (parent is Decorator decorator)
        {
            if (decorator.Child is Control child)
                yield return child;
        }
        else if (parent is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is Control c)
                    yield return c;
            }
        }
        else if (parent is ContentControl contentControl)
        {
            if (contentControl.Content is Control c)
                yield return c;
        }
        else if (parent is ScrollViewer scrollViewer)
        {
            if (scrollViewer.Content is Control c)
                yield return c;
        }
        else if (parent is Popup popup)
        {
            if (popup.Child is Control c)
                yield return c;
        }
    }

    private static void EnsureApplication()
    {
        if (Application.Current is App app)
        {
            if (!app.Resources.ContainsKey("PrimaryAccentBrush"))
                app.Initialize();
            return;
        }

        new App().Initialize();
    }
}
