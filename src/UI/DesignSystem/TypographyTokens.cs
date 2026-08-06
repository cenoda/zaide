using Avalonia;
using Avalonia.Media;

namespace Zaide.UI.DesignSystem;

/// <summary>
/// Resolves shared typography tokens from <c>App.axaml</c>.
/// Use for control properties (for example <c>Button.FontSize</c>), not
/// <see cref="TextStyles"/> text-block factories.
/// </summary>
internal static class TypographyTokens
{
    public static double FontSizeXs => GetDouble("FontSizeXs", 11d);
    public static double FontSizeSm => GetDouble("FontSizeSm", 12d);
    public static double FontSizeMd => GetDouble("FontSizeMd", 13d);
    public static double FontSizeLg => GetDouble("FontSizeLg", 15d);
    public static double FontSizeXl => GetDouble("FontSizeXl", 18d);

    public static double LineHeightSm => GetDouble("LineHeightSm", 16d);
    public static double LineHeightMd => GetDouble("LineHeightMd", 19.5d);
    public static double LineHeightLg => GetDouble("LineHeightLg", 22.5d);

    public static FontWeight FontWeightRegular => FontWeight.Normal;
    public static FontWeight FontWeightMedium => FontWeight.Medium;
    public static FontWeight FontWeightSemiBold => FontWeight.SemiBold;
    public static FontWeight FontWeightBold => FontWeight.Bold;

    private static double GetDouble(string resourceKey, double fallback)
    {
        if (Application.Current?.Resources.TryGetValue(resourceKey, out var value) == true &&
            value is double token)
        {
            return token;
        }

        return fallback;
    }
}
