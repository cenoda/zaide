using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Avalonia;
using Avalonia.Styling;
using ReactiveUI;
using ReactiveUI.Avalonia;
using ReactiveUI.Builder;
using Splat;
using Zaide.UI.DesignSystem;

namespace Zaide.Tests.Infrastructure;

/// <summary>
/// Shared, idempotent Avalonia/ReactiveUI bootstrap for the test assembly.
/// Initializes once per testhost process and exposes reset hooks for mutable
/// global state in serialized UI collections.
/// </summary>
public static class ReactiveUiTestBootstrap
{
    private static int _reactiveInitialized;
    private static readonly object Sync = new();

    private static int _applicationInitialized;
    private static Zaide.App.Composition.App? _application;

    [ModuleInitializer]
    internal static void InitializeModule()
    {
        EnsureInitialized();
        EnsureApplication();
    }

    public static void EnsureInitialized()
    {
        if (Volatile.Read(ref _reactiveInitialized) != 0)
            return;

        lock (Sync)
        {
            if (_reactiveInitialized != 0)
                return;

            try
            {
                RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
            }
            catch (InvalidOperationException)
            {
                // Another bootstrap path already initialized ReactiveUI in this process.
            }

            Volatile.Write(ref _reactiveInitialized, 1);
        }
    }

    public static Zaide.App.Composition.App EnsureApplication()
    {
        EnsureInitialized();

        lock (Sync)
        {
            if (Application.Current is Zaide.App.Composition.App current)
            {
                ThemeBinding.TestApplication = current;
                EnsureApplicationResources(current);
                EnsureDefaultServicesRegistered();
                return current;
            }

            if (_application is not null)
            {
                ThemeBinding.TestApplication = _application;
                EnsureApplicationResources(_application);
                EnsureDefaultServicesRegistered();
                return _application;
            }

            _application = new Zaide.App.Composition.App();
            _application.Initialize();
            ThemeBinding.TestApplication = _application;
            EnsureApplicationResources(_application);
            EnsureDefaultServicesRegistered();
            return _application;
        }
    }

    public static void RegisterDefaultActivationForViewFetcher()
    {
        EnsureInitialized();
        Locator.CurrentMutable.Register(
            () => new AvaloniaActivationForViewFetcher(),
            typeof(IActivationForViewFetcher));
    }

    /// <summary>
    /// Restores Splat registrations that tests may override while keeping the
    /// one-time ReactiveUI/Avalonia bootstrap intact.
    /// </summary>
    public static void ResetMutableState()
    {
        EnsureInitialized();
        RegisterDefaultActivationForViewFetcher();
    }

    private static void EnsureDefaultServicesRegistered()
    {
        if (Interlocked.Exchange(ref _applicationInitialized, 1) != 0)
            return;

        RegisterDefaultActivationForViewFetcher();
    }

    private static void EnsureApplicationResources(Zaide.App.Composition.App app)
    {
        if (!app.TryGetResource("PrimaryAccentBrush", ThemeVariant.Light, out _))
            app.Initialize();
    }
}
