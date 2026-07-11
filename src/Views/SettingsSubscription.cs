using System;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using ReactiveUI.Avalonia;
using Zaide.Models;
using Zaide.Services;

namespace Zaide.Views;

internal static class SettingsSubscription
{
    public static IDisposable Subscribe(
        ISettingsService settings,
        Action<SettingsModel> apply,
        IScheduler? scheduler = null)
    {
        return settings.WhenChanged
            .ObserveOn(scheduler ?? AvaloniaScheduler.Instance)
            .Subscribe(apply);
    }
}
