using Microsoft.Extensions.DependencyInjection;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;

namespace Zaide.App.Composition.Registration;

internal static class TownhallServiceCollectionExtensions
{
    internal static IServiceCollection AddZaideTownhall(
        this IServiceCollection services)
    {
        services.AddSingleton<TownhallState>();
        services.AddSingleton<TownhallViewModel>();

        return services;
    }
}
