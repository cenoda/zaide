using Microsoft.Extensions.DependencyInjection;
using System.Reactive.Concurrency;
using Zaide.App.Composition;
using Zaide.App.Shell;
using Zaide.Features.Workspace.Domain;

namespace Zaide.App.Composition.Registration;

internal static class AppCoreServiceCollectionExtensions
{
    internal static IServiceCollection AddZaideAppCore(
        this IServiceCollection services)
    {
        services.AddSingleton<Workspace>();
        services.AddSingleton<ICommandRegistry, CommandRegistry>();
        services.AddSingleton<StatusBarViewModel>();
        services.AddSingleton<IScheduler>(
            _ => ReactiveUI.Avalonia.AvaloniaScheduler.Instance);
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<CommandPaletteViewModel>();
        return services;
    }
}
