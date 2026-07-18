using Microsoft.Extensions.DependencyInjection;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Infrastructure;

namespace Zaide.App.Composition.Registration;

internal static class SettingsServiceCollectionExtensions
{
    internal static IServiceCollection AddZaideSettings(
        this IServiceCollection services)
    {
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ISecretStore>(_ =>
            new FileSecretStore(SettingsPathResolver.GetSecretsPath()));

        return services;
    }
}
