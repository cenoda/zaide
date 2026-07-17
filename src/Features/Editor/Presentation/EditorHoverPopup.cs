using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Zaide.UI.DesignSystem;

namespace Zaide.Features.Editor.Presentation;

/// <summary>
/// Minimal hover tooltip popup for the shared editor.
/// </summary>
public sealed class EditorHoverPopup : Popup
{
    private readonly TextBlock _textBlock;

    public EditorHoverPopup()
    {
        IsLightDismissEnabled = false;
        Placement = PlacementMode.Pointer;
        IsHitTestVisible = false;

        _textBlock = TextStyles.Caption(string.Empty);
        _textBlock.TextWrapping = TextWrapping.Wrap;
        _textBlock.MaxWidth = 420;

        var border = new Border
        {
            Background = (IBrush?)Application.Current!.Resources["SurfacePanelBrush"],
            BorderBrush = (IBrush?)Application.Current!.Resources["BorderSubtleBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = LayoutTokens.RadiusSm,
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingSm, LayoutTokens.SpacingSm - LayoutTokens.SpacingXxs),
            Child = _textBlock,
        };
        AutomationProperties.SetName(border, "Hover information");
        AutomationProperties.SetHelpText(border, "Read-only hover tooltip for the symbol under the caret.");

        Child = border;
    }

    public void SetContent(string? content) => _textBlock.Text = content ?? string.Empty;
}
