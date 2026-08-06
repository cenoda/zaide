using Avalonia;
using Avalonia.Media;

namespace Zaide.UI.DesignSystem;

/// <summary>
/// Provides theme-aware box shadow accessors for elevation levels. Shadows are
/// resolved from the active theme dictionary so they follow
/// <see cref="Application.ActualThemeVariant"/>.
/// </summary>
internal static class Elevation
{
    public static BoxShadows ShadowSm => Resolve("ShadowSm", ShadowSmFallback);
    public static BoxShadows ShadowMd => Resolve("ShadowMd", ShadowMdFallback);
    public static BoxShadows ShadowLg => Resolve("ShadowLg", ShadowLgFallback);

    private static readonly BoxShadows ShadowSmFallback =
        new(new BoxShadow { OffsetY = 1, Blur = 2, Color = Color.FromArgb(13, 0, 0, 0) });

    private static readonly BoxShadows ShadowMdFallback =
        new(new BoxShadow { OffsetY = 4, Blur = 6, Spread = -1, Color = Color.FromArgb(26, 0, 0, 0) },
            new[] { new BoxShadow { OffsetY = 2, Blur = 4, Spread = -2, Color = Color.FromArgb(15, 0, 0, 0) } });

    private static readonly BoxShadows ShadowLgFallback =
        new(new BoxShadow { OffsetY = 10, Blur = 15, Spread = -3, Color = Color.FromArgb(26, 0, 0, 0) },
            new[] { new BoxShadow { OffsetY = 4, Blur = 6, Spread = -4, Color = Color.FromArgb(15, 0, 0, 0) } });

    private static BoxShadows Resolve(string key, BoxShadows fallback)
    {
        if (Application.Current?.TryGetResource(key, ThemeBinding.CurrentVariant, out var value) == true &&
            value is BoxShadows shadows)
        {
            return shadows;
        }

        return fallback;
    }
}
