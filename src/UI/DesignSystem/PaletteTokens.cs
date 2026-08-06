using Avalonia.Media;

namespace Zaide.UI.DesignSystem;

/// <summary>
/// Resolves palette brushes and colors from the active theme variant.
/// </summary>
internal static class PaletteTokens
{
    public static IBrush TextPrimaryBrush => ThemeBinding.GetBrush("TextPrimaryBrush");

    public static IBrush PrimaryAccentBrush => ThemeBinding.GetBrush("PrimaryAccentBrush");

    public static IBrush TextSecondaryBrush => ThemeBinding.GetBrush("TextSecondaryBrush");

    public static Color SurfaceRaisedColor => ThemeBinding.GetColor("SurfaceRaisedBrushColor");

    public static Color PrimaryAccentColor => ThemeBinding.GetColor("PrimaryAccentBrushColor");

    public static IBrush SuccessBrush => ThemeBinding.GetBrush("SuccessBrush");

    public static IBrush SurfacePanelBrush => ThemeBinding.GetBrush("SurfacePanelBrush");

    public static IBrush SurfaceBaseBrush => ThemeBinding.GetBrush("SurfaceBaseBrush");

    public static IBrush SeparatorBrush => ThemeBinding.GetBrush("SeparatorBrush");

    public static IBrush WarningBrush => ThemeBinding.GetBrush("WarningBrush");

    public static Color GetColor(string resourceKey) => ThemeBinding.GetColor(resourceKey);

    public static IBrush GetBrush(string resourceKey) => ThemeBinding.GetBrush(resourceKey);
}
