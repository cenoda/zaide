using Avalonia;
using Avalonia.Media;

namespace Zaide.UI.DesignSystem;

/// <summary>
/// Resolves palette brushes and colors from <c>App.axaml</c> with the
/// exact no-resource fallback values used by Townhall presentation surfaces.
/// </summary>
internal static class PaletteTokens
{
    private static readonly Color TextPrimaryFallbackColor = Color.Parse("#E3E4F4");
    private static readonly Color TextSecondaryFallbackColor = Color.Parse("#8B95A5");
    private static readonly Color PrimaryAccentFallbackColor = Color.Parse("#066ADB");
    private static readonly Color SurfaceRaisedFallbackColor = Color.Parse("#243352");
    private static readonly Color SuccessFallbackColor = Color.Parse("#28A745");
    private static readonly Color SurfacePanelFallbackColor = Color.Parse("#1A2540");

    public static IBrush TextPrimaryBrush =>
        (IBrush?)Application.Current?.Resources["TextPrimaryBrush"]
        ?? new SolidColorBrush(TextPrimaryFallbackColor);

    public static IBrush PrimaryAccentBrush =>
        (IBrush?)Application.Current?.Resources["PrimaryAccentBrush"]
        ?? new SolidColorBrush(PrimaryAccentFallbackColor);

    public static IBrush TextSecondaryBrush =>
        (IBrush?)Application.Current?.Resources["TextSecondaryBrush"]
        ?? new SolidColorBrush(TextSecondaryFallbackColor);

    public static Color SurfaceRaisedColor =>
        GetColor("SurfaceRaisedBrushColor", SurfaceRaisedFallbackColor);

    public static Color PrimaryAccentColor =>
        GetColor("PrimaryAccentBrushColor", PrimaryAccentFallbackColor);

    public static IBrush SuccessBrush =>
        GetBrush("SuccessBrush", new SolidColorBrush(SuccessFallbackColor));

    public static IBrush SurfacePanelBrush =>
        GetBrush("SurfacePanelBrush", new SolidColorBrush(SurfacePanelFallbackColor));

    public static IBrush TextPrimaryBrushOrFallback =>
        GetBrush("TextPrimaryBrush", new SolidColorBrush(TextPrimaryFallbackColor));

    public static Color GetColor(string resourceKey, Color fallback)
    {
        if (Application.Current?.Resources.TryGetValue(resourceKey, out var value) == true &&
            value is Color color)
        {
            return color;
        }

        return fallback;
    }

    public static IBrush GetBrush(string resourceKey, IBrush fallback)
    {
        if (Application.Current?.Resources.TryGetValue(resourceKey, out var value) == true &&
            value is IBrush brush)
        {
            return brush;
        }

        return fallback;
    }

    public static IBrush CreateSuccessStatusFallbackBrush() =>
        new SolidColorBrush(SuccessFallbackColor);
}
