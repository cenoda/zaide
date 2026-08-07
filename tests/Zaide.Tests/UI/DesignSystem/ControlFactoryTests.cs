using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using Xunit;
using Zaide.Tests.Infrastructure;
using Zaide.UI.DesignSystem;

namespace Zaide.Tests.UI.DesignSystem;

[Collection("AvaloniaUiInitialization")]
public class ControlFactoryTests
{
    public ControlFactoryTests() => ReactiveUiTestBootstrap.EnsureApplication();
    [Fact]
    public void AppButton_Primary_UsesAccentTokensAndTheme()
    {
        var button = AppButton.Primary("Commit");

        Assert.NotNull(button);
        Assert.Equal("Commit", button.Content);
        Assert.IsType<ControlTheme>(button.Theme);
        Assert.Equal(ThemeBinding.GetBrush("AccentBrush"), button.Background);
        Assert.Equal(ThemeBinding.GetBrush("TextOnAccentBrush"), button.Foreground);
        Assert.Contains(
            button.Theme!.Children.OfType<Style>(),
            style => StyleUsesBrushKey(style, Button.BackgroundProperty, "AccentHoverBrush"));
    }

    [Fact]
    public void AppButton_Ghost_UsesOverlayHoverTheme()
    {
        var button = AppButton.Ghost("Settings");

        Assert.NotNull(button);
        Assert.IsType<ControlTheme>(button.Theme);
        Assert.Equal(Brushes.Transparent, button.Background);
        Assert.Contains(
            button.Theme!.Children.OfType<Style>(),
            style => StyleUsesBrushKey(style, Button.BackgroundProperty, "OverlayHoverBrush"));
        Assert.Contains(
            button.Theme.Children.OfType<Style>(),
            style => StyleUsesBrushKey(style, Button.BorderBrushProperty, "BorderFocusBrush"));
    }

    [Fact]
    public void AppButton_IconSurface_WiresInteractiveCatalog()
    {
        var surface = AppButton.IconSurface(new TextBlock { Text = "X" }, tooltip: "Explorer");

        Assert.NotNull(surface);
        Assert.Contains(ControlThemeCatalog.InteractiveClass, surface.Classes);
        Assert.IsType<ControlTheme>(surface.Theme);
        Assert.Equal("Explorer", ToolTip.GetTip(surface));
    }

    [Fact]
    public void AppTextBox_Input_UsesSurfaceRaisedAndFocusTheme()
    {
        var textBox = AppTextBox.Input("Commit message...", acceptsReturn: true, maxHeight: 120);

        Assert.NotNull(textBox);
        Assert.True(textBox.AcceptsReturn);
        Assert.Equal(120, textBox.MaxHeight);
        Assert.Equal(ThemeBinding.GetBrush("SurfaceRaised1Brush"), textBox.Background);
        Assert.IsType<ControlTheme>(textBox.Theme);
        Assert.Contains(
            textBox.Theme!.Children.OfType<Style>(),
            style => StyleUsesBrushKey(style, TextBox.BorderBrushProperty, "BorderFocusBrush"));
    }

    [Fact]
    public void AppTextBox_Search_IsStretchConfigured()
    {
        var textBox = AppTextBox.Search("Type a command...");

        Assert.NotNull(textBox);
        Assert.Equal(HorizontalAlignment.Stretch, textBox.HorizontalAlignment);
        Assert.Equal("Type a command...", textBox.PlaceholderText);
    }

    [Fact]
    public void ListRow_Create_AppliesInteractiveSurface()
    {
        var child = new TextBlock { Text = "row" };
        var row = ListRow.Create(child, tag: "node");

        Assert.NotNull(row);
        Assert.Same(child, row.Child);
        Assert.Equal("node", row.Tag);
        Assert.Contains(ControlThemeCatalog.InteractiveClass, row.Classes);
        Assert.IsType<ControlTheme>(row.Theme);
    }

    [Fact]
    public void ListRow_SetSelected_TogglesSelectedClass()
    {
        var row = ListRow.Create(new TextBlock { Text = "row" });

        ListRow.SetSelected(row, true);
        Assert.Contains(ControlThemeCatalog.SelectedClass, row.Classes);

        ListRow.SetSelected(row, false);
        Assert.DoesNotContain(ControlThemeCatalog.SelectedClass, row.Classes);
    }

    [Fact]
    public void PanelChrome_DividerAndEmptyState_UseSemanticTokens()
    {
        var divider = PanelChrome.Divider();
        var empty = PanelChrome.EmptyState("No items");

        Assert.NotNull(divider);
        Assert.Equal(ThemeBinding.GetBrush("SeparatorBrush"), divider.Background);
        Assert.NotNull(empty);
        Assert.Equal("No items", empty.Text);
        Assert.NotNull(empty.Foreground);
    }

    [Fact]
    public void PanelChrome_SectionHeader_IncludesTrailingControl()
    {
        var trailing = AppButton.Icon(new TextBlock { Text = "+" });
        var header = PanelChrome.SectionHeader("Source Control", trailing);

        Assert.NotNull(header);
        Assert.Equal(2, header.Children.Count);
        Assert.Same(trailing, header.Children[1]);
    }

    private static bool StyleUsesBrushKey(Style style, AvaloniaProperty property, string brushKey) =>
        style.Setters
            .OfType<Setter>()
            .Any(s =>
                s.Property == property &&
                s.Value is DynamicResourceExtension { ResourceKey: var key } &&
                key is string resourceKey &&
                resourceKey == brushKey);
}
