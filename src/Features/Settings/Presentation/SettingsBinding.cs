using System;
using System.Reactive.Concurrency;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;

namespace Zaide.Features.Settings.Presentation;

internal sealed class SettingsBinding : IDisposable
{
    private IDisposable? _subscription;

    public SettingsBinding(ISettingsService settings, Action<SettingsModel> apply)
        : this(settings, apply, null)
    {
    }

    internal SettingsBinding(
        ISettingsService settings,
        Action<SettingsModel> apply,
        IScheduler? scheduler)
    {
        apply(settings.Current);
        _subscription = SettingsSubscription.Subscribe(settings, apply, scheduler);
    }

    public void Dispose()
    {
        System.Threading.Interlocked.Exchange(ref _subscription, null)?.Dispose();
    }
}
