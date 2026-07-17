using System;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Styling;
using Xunit;
using Zaide.App.Shell;

namespace Zaide.Tests.App.Shell;
public class AnimationsTests
{
    [Fact]
    public void FadeIn_DefaultDuration_Is180ms()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(180), Animations.FadeIn().Duration);
    }

    [Fact]
    public void FadeOut_DefaultDuration_Is180ms()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(180), Animations.FadeOut().Duration);
    }

    [Fact]
    public void FadeIn_UsesCubicEaseOut()
    {
        Assert.IsType<CubicEaseOut>(Animations.FadeIn().Easing);
    }

    [Fact]
    public void FadeOut_UsesCubicEaseIn()
    {
        Assert.IsType<CubicEaseIn>(Animations.FadeOut().Easing);
    }

    [Fact]
    public void SlideIn_DefaultDuration_Is180ms()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(180), Animations.SlideIn(HorizontalDirection.Left).Duration);
    }

    [Fact]
    public void SlideOut_DefaultDuration_Is180ms()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(180), Animations.SlideOut(HorizontalDirection.Right).Duration);
    }

    [Fact]
    public void SlideIn_UsesCubicEaseOut()
    {
        Assert.IsType<CubicEaseOut>(Animations.SlideIn(HorizontalDirection.Left).Easing);
    }

    [Fact]
    public void SlideOut_UsesCubicEaseIn()
    {
        Assert.IsType<CubicEaseIn>(Animations.SlideOut(HorizontalDirection.Right).Easing);
    }

    [Fact]
    public void SlideIn_Left_StartsNegativeX()
    {
        var animation = Animations.SlideIn(HorizontalDirection.Left);
        Assert.Equal(-12d, GetDoubleSetterValue(animation, 0, TranslateTransform.XProperty));
        Assert.Equal(0d, GetDoubleSetterValue(animation, 1, TranslateTransform.XProperty));
    }

    [Fact]
    public void SlideOut_Right_EndsPositiveX()
    {
        var animation = Animations.SlideOut(HorizontalDirection.Right);
        Assert.Equal(0d, GetDoubleSetterValue(animation, 0, TranslateTransform.XProperty));
        Assert.Equal(12d, GetDoubleSetterValue(animation, 1, TranslateTransform.XProperty));
    }

    [Fact]
    public void CreateScaleBounce_Uses180msAndCubicEaseOut()
    {
        var animation = Animations.CreateScaleBounce();

        Assert.Equal(TimeSpan.FromMilliseconds(180), animation.Duration);
        Assert.IsType<CubicEaseOut>(animation.Easing);
        Assert.Equal(0.95d, GetDoubleSetterValue(animation, 0, ScaleTransform.ScaleXProperty));
        Assert.Equal(1d, GetDoubleSetterValue(animation, 1, ScaleTransform.ScaleXProperty));
    }

    private static double GetDoubleSetterValue(Animation animation, int frameIndex, AvaloniaProperty property)
    {
        var setter = animation.Children[frameIndex].Setters
            .OfType<Setter>()
            .Single(s => s.Property == property);
        return Assert.IsType<double>(setter.Value);
    }
}
