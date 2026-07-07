using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Zaide.Views;

/// <summary>
/// Shared motion helpers for Refactor 4 M6.
/// </summary>
public static class Animations
{
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan HoverDuration = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan PanelSwitchDuration = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan TabSwitchDuration = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan SendButtonBounceDuration = TimeSpan.FromMilliseconds(180);

    public static Animation FadeIn(TimeSpan? duration = null)
    {
        return CreateOpacityAnimation(0d, 1d, duration ?? DefaultDuration, new CubicEaseOut());
    }

    public static Animation FadeOut(TimeSpan? duration = null)
    {
        return CreateOpacityAnimation(1d, 0d, duration ?? DefaultDuration, new CubicEaseIn());
    }

    public static Animation SlideIn(HorizontalDirection dir, TimeSpan? duration = null)
    {
        var offset = dir == HorizontalDirection.Left ? -12d : 12d;
        return CreateTranslateAnimation(offset, 0d, duration ?? DefaultDuration, new CubicEaseOut());
    }

    public static Animation SlideOut(HorizontalDirection dir, TimeSpan? duration = null)
    {
        var offset = dir == HorizontalDirection.Left ? -12d : 12d;
        return CreateTranslateAnimation(0d, offset, duration ?? DefaultDuration, new CubicEaseIn());
    }

    public static void Transition(Visual target, Animation animation)
    {
        _ = animation.RunAsync(target);
    }

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

    internal static Animation HoverBackground(IBrush from, IBrush to)
    {
        return CreateBrushAnimation(Border.BackgroundProperty, from, to, HoverDuration, new CubicEaseOut());
    }

    internal static Animation TabFadeIn()
    {
        return CreateOpacityAnimation(0.72d, 1d, TabSwitchDuration, new CubicEaseOut());
    }

    internal static Animation TabFadeOut()
    {
        return CreateOpacityAnimation(1d, 0.72d, TabSwitchDuration, new CubicEaseOut());
    }

    internal static Animation PanelEnter(HorizontalDirection dir)
    {
        var offset = dir == HorizontalDirection.Left ? -12d : 12d;
        return CreateOpacityTranslateAnimation(
            0d,
            1d,
            offset,
            0d,
            PanelSwitchDuration,
            new CubicEaseOut());
    }

    internal static Animation PanelExit(HorizontalDirection dir)
    {
        var offset = dir == HorizontalDirection.Left ? -12d : 12d;
        return CreateOpacityTranslateAnimation(
            1d,
            0d,
            0d,
            offset,
            PanelSwitchDuration,
            new CubicEaseIn());
    }

    internal static Animation NavEnter(HorizontalDirection dir)
    {
        var offset = dir == HorizontalDirection.Left ? -12d : 12d;
        return CreateOpacityTranslateAnimation(
            0d,
            1d,
            offset,
            0d,
            DefaultDuration,
            new CubicEaseOut());
    }

    internal static Animation NavExit(HorizontalDirection dir)
    {
        var offset = dir == HorizontalDirection.Left ? -12d : 12d;
        return CreateOpacityTranslateAnimation(
            1d,
            0d,
            0d,
            offset,
            DefaultDuration,
            new CubicEaseIn());
    }

    internal static async Task RunAsync(Animatable target, Animation animation)
    {
        await animation.RunAsync(target);
    }

    private static Animation CreateOpacityAnimation(double from, double to, TimeSpan duration, Easing easing)
    {
        return new Animation
        {
            Duration = duration,
            Easing = easing,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters = { new Setter(Visual.OpacityProperty, from) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters = { new Setter(Visual.OpacityProperty, to) }
                }
            }
        };
    }

    private static Animation CreateTranslateAnimation(double from, double to, TimeSpan duration, Easing easing)
    {
        return new Animation
        {
            Duration = duration,
            Easing = easing,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters = { new Setter(TranslateTransform.XProperty, from) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters = { new Setter(TranslateTransform.XProperty, to) }
                }
            }
        };
    }

    private static Animation CreateBrushAnimation(AvaloniaProperty property, IBrush from, IBrush to, TimeSpan duration, Easing easing)
    {
        return new Animation
        {
            Duration = duration,
            Easing = easing,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters = { new Setter(property, from) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters = { new Setter(property, to) }
                }
            }
        };
    }

    private static Animation CreateOpacityTranslateAnimation(
        double fromOpacity,
        double toOpacity,
        double fromTranslateX,
        double toTranslateX,
        TimeSpan duration,
        Easing easing)
    {
        return new Animation
        {
            Duration = duration,
            Easing = easing,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters =
                    {
                        new Setter(Visual.OpacityProperty, fromOpacity),
                        new Setter(TranslateTransform.XProperty, fromTranslateX)
                    }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters =
                    {
                        new Setter(Visual.OpacityProperty, toOpacity),
                        new Setter(TranslateTransform.XProperty, toTranslateX)
                    }
                }
            }
        };
    }
}
