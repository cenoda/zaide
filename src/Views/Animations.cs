using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Styling;
using Avalonia.Media;
using System;

namespace Zaide.Views;

/// <summary>
/// Shared animations used by views.
/// Public API must remain stable for M6.
/// </summary>
public static class Animations
{
    private static readonly TimeSpan DefaultFadeDuration = TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan DefaultSlideDuration = TimeSpan.FromMilliseconds(220);
    private static readonly TimeSpan SendButtonBounceDuration = TimeSpan.FromMilliseconds(180);

    /// <summary>
    /// Creates a fade-in animation for opacity.
    /// </summary>
    public static Animation FadeIn()
    {
        return new Animation
        {
            Duration = DefaultFadeDuration,
            Easing = new CubicEaseOut(),
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters = { new Setter(Visual.OpacityProperty, 0d) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters = { new Setter(Visual.OpacityProperty, 1d) }
                }
            }
        };
    }

    /// <summary>
    /// Creates a fade-out animation for opacity.
    /// </summary>
    public static Animation FadeOut()
    {
        return new Animation
        {
            Duration = DefaultFadeDuration,
            Easing = new CubicEaseOut(),
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters = { new Setter(Visual.OpacityProperty, 1d) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters = { new Setter(Visual.OpacityProperty, 0d) }
                }
            }
        };
    }

    /// <summary>
    /// Creates a slide-in animation along Y axis.
    /// </summary>
    public static Animation SlideIn(double fromY = 8d, double toY = 0d)
    {
        return new Animation
        {
            Duration = DefaultSlideDuration,
            Easing = new CubicEaseOut(),
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters =
                    {
                        new Setter(TranslateTransform.YProperty, fromY)
                    }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters =
                    {
                        new Setter(TranslateTransform.YProperty, toY)
                    }
                }
            }
        };
    }

    /// <summary>
    /// Creates a slide-out animation along Y axis.
    /// </summary>
    public static Animation SlideOut(double fromY = 0d, double toY = 8d)
    {
        return new Animation
        {
            Duration = DefaultSlideDuration,
            Easing = new CubicEaseOut(),
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters =
                    {
                        new Setter(TranslateTransform.YProperty, fromY)
                    }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters =
                    {
                        new Setter(TranslateTransform.YProperty, toY)
                    }
                }
            }
        };
    }

    /// <summary>
    /// Composes a transition animation from provided keyframes.
    /// </summary>
    public static Animation Transition(TimeSpan duration, params KeyFrame[] keyFrames)
    {
        var animation = new Animation
        {
            Duration = duration,
            Easing = new CubicEaseOut()
        };

        foreach (var frame in keyFrames)
        {
            animation.Children.Add(frame);
        }

        return animation;
    }

    /// <summary>
    /// Internal helper for Townhall send-button press bounce.
    /// </summary>
    internal static Animation CreateScaleBounce(double pressedScale = 0.95d, double normalScale = 1d)
    {
        return new Animation
        {
            Duration = SendButtonBounceDuration,
            Easing = new CubicEaseOut(),
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters =
                    {
                        new Setter(ScaleTransform.ScaleXProperty, pressedScale),
                        new Setter(ScaleTransform.ScaleYProperty, pressedScale)
                    }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters =
                    {
                        new Setter(ScaleTransform.ScaleXProperty, normalScale),
                        new Setter(ScaleTransform.ScaleYProperty, normalScale)
                    }
                }
            }
        };
    }
}
