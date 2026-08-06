using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Zaide.UI.DesignSystem
{
    public static class TextStyles
    {
        private static readonly Color PrimaryFallbackColor = Color.Parse("#E3E4F4");
        private static readonly Color SecondaryFallbackColor = Color.Parse("#8B95A5");
        private static readonly Color PrimaryAccentFallbackColor = Color.Parse("#066ADB");

        /// <summary>
        /// Test-only application resolver. Defaults to <see cref="Application.Current"/>.
        /// Tests override this to exercise the navy fallback path deterministically
        /// without mutating global Avalonia state.
        /// </summary>
        internal static Func<Application?>? ApplicationResolver { get; set; }

        private static IBrush ResolveBrush(string colorKey, Color fallback)
        {
            var application = (ApplicationResolver ?? (() => Application.Current))();
            if (application is null)
            {
                // No application: fall back to the navy palette rather than
                // throwing, so views always render readable text.
                return new SolidColorBrush(fallback);
            }

            try
            {
                // Theme-aware, resolved from the active variant. Reading a
                // Color resource (rather than a dispatcher-owned brush) keeps
                // this safe off the UI thread.
                return new SolidColorBrush(ThemeBinding.GetColor(colorKey));
            }
            catch (InvalidOperationException)
            {
                return new SolidColorBrush(fallback);
            }
        }

        public static TextBlock Header(string text) => new()
        {
            Text = text,
            FontSize = TypographyTokens.FontSizeMd,
            FontWeight = TypographyTokens.FontWeightSemiBold,
            Foreground = ResolveBrush("TextPrimaryBrushColor", PrimaryFallbackColor)
        };

        public static TextBlock Body(string text) => new()
        {
            Text = text,
            FontSize = TypographyTokens.FontSizeMd,
            FontWeight = TypographyTokens.FontWeightRegular,
            Foreground = ResolveBrush("TextPrimaryBrushColor", PrimaryFallbackColor)
        };

        public static TextBlock Caption(string text) => new()
        {
            Text = text,
            FontSize = TypographyTokens.FontSizeXs,
            FontWeight = TypographyTokens.FontWeightRegular,
            Foreground = ResolveBrush("TextSecondaryBrushColor", SecondaryFallbackColor)
        };

        public static TextBlock Brand(string text) => new()
        {
            Text = text,
            FontSize = TypographyTokens.FontSizeSm,
            FontWeight = TypographyTokens.FontWeightSemiBold,
            Foreground = ResolveBrush("PrimaryAccentBrushColor", PrimaryAccentFallbackColor)
        };
    }
}
