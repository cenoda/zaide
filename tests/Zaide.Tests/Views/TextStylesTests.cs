using Xunit;
using Avalonia.Controls;
using Avalonia.Media;
using Zaide.Styles;
using Avalonia;
using System;

namespace Zaide.Tests.Views
{
    public class TextStylesTests
    {
        [Fact]
        public void Header_UsesHeaderSizeAndWeight()
        {
            // Arrange & Act
            var textBlock = TextStyles.Header("Test Header");

            // Assert
            Assert.Equal(13, (int)textBlock.FontSize);
            Assert.Equal(FontWeight.SemiBold, textBlock.FontWeight);
            Assert.Equal("Test Header", textBlock.Text);
        }

        [Fact]
        public void Body_UsesBodySizeAndWeight()
        {
            // Arrange & Act
            var textBlock = TextStyles.Body("Test Body");

            // Assert
            Assert.Equal(13, (int)textBlock.FontSize);
            Assert.Equal(FontWeight.Normal, textBlock.FontWeight);
            Assert.Equal("Test Body", textBlock.Text);
        }

        [Fact]
        public void Caption_UsesCaptionSizeAndWeight()
        {
            // Arrange & Act
            var textBlock = TextStyles.Caption("Test Caption");

            // Assert
            Assert.Equal(11, (int)textBlock.FontSize);
            Assert.Equal(FontWeight.Normal, textBlock.FontWeight);
            Assert.Equal("Test Caption", textBlock.Text);
        }

        [Fact]
        public void Brand_UsesBrandSizeAndWeight()
        {
            // Arrange & Act
            var textBlock = TextStyles.Brand("Test Brand");

            // Assert
            Assert.Equal(12, (int)textBlock.FontSize);
            Assert.Equal(FontWeight.SemiBold, textBlock.FontWeight);
            Assert.Equal("Test Brand", textBlock.Text);
        }

        [Fact]
        public void FactoryReturns_DistinctInstances()
        {
            // Arrange & Act
            var header1 = TextStyles.Header("Header");
            var header2 = TextStyles.Header("Header");
            var body1 = TextStyles.Body("Body");
            var caption1 = TextStyles.Caption("Caption");
            var brand1 = TextStyles.Brand("Brand");

            // Assert - All should be distinct instances
            Assert.NotSame(header1, header2);
            Assert.NotSame(body1, header1);
            Assert.NotSame(caption1, body1);
            Assert.NotSame(brand1, caption1);

            var caption2 = TextStyles.Caption("Caption");
            Assert.NotSame(caption1, caption2);
        }

        [Fact]
        public void Foreground_IsNonNullIBrush_ForEveryStyle()
        {
            // Sanity: every TextStyles method must yield a non-null Foreground
            // regardless of whether Avalonia resources are present. Without
            // this, the views would render invisible text in any environment
            // where Application.Current is null.
            Assert.NotNull(TextStyles.Header("h").Foreground);
            Assert.NotNull(TextStyles.Body("b").Foreground);
            Assert.NotNull(TextStyles.Caption("c").Foreground);
            Assert.NotNull(TextStyles.Brand("br").Foreground);
        }

        [Fact]
        public void Foreground_IsSolidColorBrush_ForEveryStyle()
        {
            // The M2 fallback path must produce a SolidColorBrush (not a
            // resource key or null), so the text is always renderable.
            Assert.IsType<SolidColorBrush>(TextStyles.Header("h").Foreground);
            Assert.IsType<SolidColorBrush>(TextStyles.Body("b").Foreground);
            Assert.IsType<SolidColorBrush>(TextStyles.Caption("c").Foreground);
            Assert.IsType<SolidColorBrush>(TextStyles.Brand("br").Foreground);
        }

        [Fact]
        public void Foreground_Header_FallsBackToNavyPrimary_WhenNoResource()
        {
            // Without Avalonia resources, Header falls back to #E3E4F4
            // (TextPrimaryBrush fallback). This proves the fallback resolves
            // to the navy palette, not an arbitrary color.
            var header = TextStyles.Header("h");
            var brush = Assert.IsType<SolidColorBrush>(header.Foreground);
            Assert.Equal(Color.Parse("#E3E4F4"), brush.Color);
        }

        [Fact]
        public void Foreground_Body_FallsBackToNavyPrimary_WhenNoResource()
        {
            var body = TextStyles.Body("b");
            var brush = Assert.IsType<SolidColorBrush>(body.Foreground);
            Assert.Equal(Color.Parse("#E3E4F4"), brush.Color);
        }

        [Fact]
        public void Foreground_Caption_FallsBackToMutedSecondary_WhenNoResource()
        {
            // Caption must use the muted secondary color, not the primary
            // text color. This guards against accidentally routing Caption
            // through TextPrimaryBrush.
            var caption = TextStyles.Caption("c");
            var brush = Assert.IsType<SolidColorBrush>(caption.Foreground);
            Assert.Equal(Color.Parse("#8B95A5"), brush.Color);
        }

        [Fact]
        public void Foreground_Brand_FallsBackToAccent_WhenNoResource()
        {
            // Brand must use the accent color (PrimaryAccentBrush fallback),
            // not the primary text color. Without this check, the "powered by
            // Zaide" app name would lose its brand color.
            var brand = TextStyles.Brand("br");
            var brush = Assert.IsType<SolidColorBrush>(brand.Foreground);
            Assert.Equal(Color.Parse("#066ADB"), brush.Color);
        }

        [Fact]
        public void Foreground_Caption_AndBrand_AreDistinct()
        {
            // Caption is muted, Brand is vivid. The four styles must use
            // three distinct brush roles -- not one. This guards the M2
            // typography hierarchy: muted vs. accent vs. primary.
            var captionColor = ((SolidColorBrush)TextStyles.Caption("c").Foreground!).Color;
            var brandColor = ((SolidColorBrush)TextStyles.Brand("br").Foreground!).Color;
            var headerColor = ((SolidColorBrush)TextStyles.Header("h").Foreground!).Color;

            Assert.NotEqual(captionColor, brandColor);
            Assert.NotEqual(captionColor, headerColor);
            Assert.NotEqual(brandColor, headerColor);
        }

        [Fact]
        public void Foreground_HeaderAndBody_SharePrimaryColor()
        {
            // Header and Body are both primary text styles. They must
            // resolve to the same fallback color so the typography
            // hierarchy is consistent.
            var headerColor = ((SolidColorBrush)TextStyles.Header("h").Foreground!).Color;
            var bodyColor = ((SolidColorBrush)TextStyles.Body("b").Foreground!).Color;
            Assert.Equal(headerColor, bodyColor);
        }

        [Fact]
        public void AllMethods_ReturnSameStyle_Consistently()
        {
            // Verify that calling the same factory method with the same text
            // produces equivalent TextBlock instances in terms of style properties

            // Arrange & Act
            var headerA = TextStyles.Header("A");
            var headerB = TextStyles.Header("B");

            // Assert - FontSize and FontWeight should be same regardless of text
            Assert.Equal(headerA.FontSize, headerB.FontSize);
            Assert.Equal(headerA.FontWeight, headerB.FontWeight);

            var bodyA = TextStyles.Body("A");
            var bodyB = TextStyles.Body("B");
            Assert.Equal(bodyA.FontSize, bodyB.FontSize);
            Assert.Equal(bodyA.FontWeight, bodyB.FontWeight);
        }

        [Fact]
        public void TextBlock_Properties_CorrectlyAssigned()
        {
            // Verify that common TextBlock properties are correctly forwarded

            // Arrange & Act
            var header = TextStyles.Header("Test");

            // Assert - Text property is set
            Assert.Equal("Test", header.Text);

            // The TextWrapping default should be NoWrap
            Assert.Equal(TextWrapping.NoWrap, header.TextWrapping);

            // TextTrimming should default to None
            Assert.Equal(TextTrimming.None, header.TextTrimming);
        }

        [Fact]
        public void Header_HeaderStyle_ConsistentAcrossCalls()
        {
            // Verify Header consistently produces SemiBold weight
            var h1 = TextStyles.Header("one");
            var h2 = TextStyles.Header("two");
            var h3 = TextStyles.Header("three");

            Assert.Equal(FontWeight.SemiBold, h1.FontWeight);
            Assert.Equal(FontWeight.SemiBold, h2.FontWeight);
            Assert.Equal(FontWeight.SemiBold, h3.FontWeight);
            Assert.Equal(13, (int)h1.FontSize);
            Assert.Equal(13, (int)h2.FontSize);
        }

        [Fact]
        public void Brand_AccentStyle_ConsistentAcrossCalls()
        {
            // Verify Brand consistently produces SemiBold weight
            var b1 = TextStyles.Brand("b1");
            var b2 = TextStyles.Brand("b2");

            Assert.Equal(FontWeight.SemiBold, b1.FontWeight);
            Assert.Equal(FontWeight.SemiBold, b2.FontWeight);
            Assert.Equal(12, (int)b1.FontSize);
            Assert.Equal(12, (int)b2.FontSize);
        }

        [Fact]
        public void BodyAndHeader_ShareFontSize_DifferentWeight()
        {
            // Body and Header both use FontSize=13 but differ in weight
            var body = TextStyles.Body("");
            var header = TextStyles.Header("");

            Assert.Equal(body.FontSize, header.FontSize);
            Assert.NotEqual(body.FontWeight, header.FontWeight);
        }
    }
}
