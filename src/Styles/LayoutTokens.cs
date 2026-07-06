using Avalonia;

namespace Zaide.Styles;

/// <summary>
/// Resolves shared spacing and corner-radius tokens from <c>App.axaml</c>.
/// </summary>
public static class LayoutTokens
{
    public static double SpacingXxs => GetDouble("SpacingXxs", 2d);
    public static double SpacingXs => GetDouble("SpacingXs", 4d);
    public static double SpacingSm => GetDouble("SpacingSm", 8d);
    public static double SpacingMd => GetDouble("SpacingMd", 12d);
    public static double SpacingLg => GetDouble("SpacingLg", 16d);
    public static double SpacingXl => GetDouble("SpacingXl", 20d);
    public static double SpacingXxl => GetDouble("SpacingXxl", 24d);
    public static double SpacingNone => 0d;

    public static Thickness NoneThickness => default;
    public static CornerRadius NoneRadius => default;

    public static CornerRadius RadiusSm => GetCornerRadius("RadiusSm", 4d);
    public static CornerRadius RadiusMd => GetCornerRadius("RadiusMd", 8d);
    public static CornerRadius RadiusLg => GetCornerRadius("RadiusLg", 12d);
    public static CornerRadius RadiusXl => GetCornerRadius("RadiusXl", 16d);
    public static CornerRadius RadiusFull => GetCornerRadius("RadiusFull", 9999d);

    public static Thickness Uniform(double uniformLength) =>
        new(uniformLength);

    public static Thickness Horizontal(double horizontal) =>
        new(horizontal, 0, horizontal, 0);

    public static Thickness Vertical(double vertical) =>
        new(0, vertical, 0, vertical);

    public static Thickness Symmetric(double horizontal, double vertical) =>
        new(horizontal, vertical, horizontal, vertical);

    public static Thickness Inset(double left, double top, double right, double bottom) =>
        new(left, top, right, bottom);

    private static double GetDouble(string resourceKey, double fallback)
    {
        if (Application.Current?.Resources.TryGetValue(resourceKey, out var value) == true &&
            value is double token)
        {
            return token;
        }

        return fallback;
    }

    private static CornerRadius GetCornerRadius(string resourceKey, double fallback)
    {
        if (Application.Current?.Resources.TryGetValue(resourceKey, out var value) == true &&
            value is CornerRadius token)
        {
            return token;
        }

        return new CornerRadius(fallback);
    }
}
