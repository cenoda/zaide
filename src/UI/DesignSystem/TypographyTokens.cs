using Avalonia;

namespace Zaide.UI.DesignSystem;

/// <summary>
/// Resolves shared typography tokens from <c>App.axaml</c>.
/// Use for control properties (for example <c>Button.FontSize</c>), not
/// <see cref="TextStyles"/> text-block factories.
/// </summary>
internal static class TypographyTokens
{
    public static double FontSizeSm => GetDouble("FontSizeSm", 12d);

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
