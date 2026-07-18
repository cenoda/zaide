using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Avalonia.Splat;
using System;
using Zaide.App.Composition.Registration;

namespace Zaide.App.Composition;
class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    /// <summary>
    /// Registers all production services in the dependency-injection container.
    /// Extracted from <see cref="BuildAvaloniaApp"/> so unit tests can build an
    /// identical container and verify resolution without re-declaring every
    /// registration.
    /// </summary>
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddZaideAppCore();
        services.AddZaideSettings();
        services.AddZaideWorkspace();
        services.AddZaideEditor();
        services.AddZaideTerminal();
        services.AddZaideAgents();
        services.AddZaideTownhall();
        services.AddZaideSourceControl();
        services.AddZaideProjectSystem();
        services.AddZaideLanguage();
        services.AddZaideDebugging();

        services.AddLogging(builder => builder.AddConsole());
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseReactiveUIWithMicrosoftDependencyResolver(
                containerConfig: ConfigureServices,
                withResolver: sp =>
                {
                    App.Services = sp!;
                })
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
