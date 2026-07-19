using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Zaide.UI.DesignSystem;

namespace Zaide.Features.Townhall.Presentation;

/// <summary>
/// Builds the shared Townhall avatar treatment used by the people and chat panels.
/// </summary>
public static class TownhallAvatarFactory
{
    public static Control Create(string displayName, string statusBrushKey, double size, double statusDotSize)
    {
        var initials = string.IsNullOrWhiteSpace(displayName)
            ? "?"
            : displayName[..1].ToUpperInvariant();

        var baseColor = PaletteTokens.SurfaceRaisedColor;
        var accentColor = PaletteTokens.PrimaryAccentColor;
        var statusBrush = PaletteTokens.GetBrush(
            statusBrushKey,
            PaletteTokens.CreateSuccessStatusFallbackBrush());
        var surfaceBrush = PaletteTokens.SurfacePanelBrush;

        var initialsText = TextStyles.Body(initials);
        initialsText.HorizontalAlignment = HorizontalAlignment.Center;
        initialsText.VerticalAlignment = VerticalAlignment.Center;
        initialsText.Foreground = PaletteTokens.TextPrimaryBrushOrFallback;

        var avatarBackground = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = LayoutTokens.RadiusFull,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Lighten(baseColor, 0.05), 0),
                    new GradientStop(baseColor, 1)
                }
            },
            Child = initialsText
        };

        var innerRing = new Border
        {
            Width = size - 2,
            Height = size - 2,
            // M5-allow: The avatar ring is inset by 1px so the stroke stays fully inside the circular fill.
            Margin = LayoutTokens.Uniform(1),
            CornerRadius = LayoutTokens.RadiusFull,
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(
                0x4D,
                accentColor.R,
                accentColor.G,
                accentColor.B)),
            IsHitTestVisible = false
        };

        var statusDot = new Border
        {
            Width = statusDotSize,
            Height = statusDotSize,
            CornerRadius = LayoutTokens.RadiusFull,
            Background = statusBrush,
            BorderThickness = new Thickness(1),
            BorderBrush = surfaceBrush,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            // M5-allow: The status dot uses a 1px offset so its outline tucks into the avatar edge cleanly.
            Margin = LayoutTokens.Inset(0, 0, 1, 1),
            IsHitTestVisible = false
        };

        return new Grid
        {
            Width = size,
            Height = size,
            Children =
            {
                avatarBackground,
                innerRing,
                statusDot
            }
        };
    }

    private static Color Lighten(Color color, double amount)
    {
        byte Channel(byte channel) => (byte)(channel + ((255 - channel) * amount));

        return Color.FromArgb(
            color.A,
            Channel(color.R),
            Channel(color.G),
            Channel(color.B));
    }

}
