using Microsoft.Extensions.DependencyInjection;
using Zaide.Features.Terminal.Contracts;
using Zaide.Features.Terminal.Infrastructure;
using Zaide.Features.Terminal.Presentation;

namespace Zaide.App.Composition.Registration;

internal static class TerminalServiceCollectionExtensions
{
    internal static IServiceCollection AddZaideTerminal(
        this IServiceCollection services)
    {
        services.AddSingleton<ITerminalServiceFactory, LinuxTerminalServiceFactory>();
        services.AddSingleton<ITerminalHost, TerminalHost>();

        return services;
    }
}
