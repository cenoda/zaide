using System;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using ReactiveUI.Avalonia;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;

namespace Zaide.Features.Settings.Presentation;

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
