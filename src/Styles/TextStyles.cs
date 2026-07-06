using Avalonia.Controls;
using Avalonia.Media;
using System;
using Avalonia;

namespace Zaide.Styles
{
    public static class TextStyles
    {
        private static IBrush GetPrimaryBrush() => 
            (IBrush?)Application.Current?.Resources["TextPrimaryBrush"] ?? new SolidColorBrush(Avalonia.Media.Color.Parse("#E3E4F4"));

        private static IBrush GetSecondaryBrush() => 
            (IBrush?)Application.Current?.Resources["TextSecondaryBrush"] ?? new SolidColorBrush(Avalonia.Media.Color.Parse("#8B95A5"));

        private static IBrush GetPrimaryAccentBrush() => 
            (IBrush?)Application.Current?.Resources["PrimaryAccentBrush"] ?? new SolidColorBrush(Avalonia.Media.Color.Parse("#066ADB"));

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