using Avalonia.Controls;
using Avalonia.Media;
using System;
using Avalonia;

namespace Zaide.UI.DesignSystem
{
    public static class TextStyles
    {
        private static readonly Color PrimaryFallbackColor = Color.Parse("#E3E4F4");
        private static readonly Color SecondaryFallbackColor = Color.Parse("#8B95A5");
        private static readonly Color PrimaryAccentFallbackColor = Color.Parse("#066ADB");

        private static IBrush GetPrimaryBrush() =>
            ResolveBrush("TextPrimaryBrush", PrimaryFallbackColor);

        private static IBrush GetSecondaryBrush() =>
            ResolveBrush("TextSecondaryBrush", SecondaryFallbackColor);

        private static IBrush GetPrimaryAccentBrush() =>
            ResolveBrush("PrimaryAccentBrush", PrimaryAccentFallbackColor);

        private static IBrush ResolveBrush(string resourceKey, Color fallback)
        {
            try
            {
                if (Application.Current?.Resources[resourceKey] is SolidColorBrush resourceBrush)
                {
                    // Return a detached copy so unit tests do not observe
                    // dispatcher-owned brushes created by a shared Application.
                    return new SolidColorBrush(resourceBrush.Color, resourceBrush.Opacity);
                }
            }
            catch (InvalidOperationException)
            {
                // Unit tests may share an Application created on another
                // dispatcher thread. In that case the palette fallback is safer
                // than touching dispatcher-owned resources.
            }

            return new SolidColorBrush(fallback);
        }

        public static TextBlock Header(string text) => new()
        {
            Text = text,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = GetPrimaryBrush()
        };

        public static TextBlock Body(string text) => new()
        {
            Text = text,
            FontSize = 13,
            FontWeight = FontWeight.Normal,
            Foreground = GetPrimaryBrush()
        };

        public static TextBlock Caption(string text) => new()
        {
            Text = text,
            FontSize = 11,
            FontWeight = FontWeight.Normal,
            Foreground = GetSecondaryBrush()
        };

        public static TextBlock Brand(string text) => new()
        {
            Text = text,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = GetPrimaryAccentBrush()
        };
    }
}
