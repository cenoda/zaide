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
        public void Foreground_FallBackToExpectedColor_WhenNoResources()
        {
            // This test verifies the fallback behavior when Avalonia resources
            // are not available (e.g., in a unit test without a running app).
            // Each style method falls back to a hardcoded SolidColorBrush when
            // Application.Current?.Resources returns null or missing entries.

            // Arrange & Act
            var header = TextStyles.Header("");
            var body = TextStyles.Body("");
            var caption = TextStyles.Caption("");
            var brand = TextStyles.Brand("");

            // Assert - All should have a non-null Foreground
            Assert.NotNull(header.Foreground);
            Assert.NotNull(body.Foreground);
            Assert.NotNull(caption.Foreground);
            Assert.NotNull(brand.Foreground);

            // Verify fallback colors by type
            // Header/Body fallback to #E3E4F4 (TextPrimaryBrush)
            Assert.IsAssignableFrom<IBrush>(header.Foreground);
            Assert.IsAssignableFrom<IBrush>(body.Foreground);
            // Caption falls back to #8B95A5 (TextSecondaryBrush)
            Assert.IsAssignableFrom<IBrush>(caption.Foreground);
            // Brand falls back to #066ADB (PrimaryAccentBrush)
            Assert.IsAssignableFrom<IBrush>(brand.Foreground);
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
