using Microsoft.Extensions.DependencyInjection;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Infrastructure;
using Zaide.Features.Settings.Presentation;

namespace Zaide.App.Composition.Registration;

internal static class SettingsServiceCollectionExtensions
{
    internal static IServiceCollection AddZaideSettings(
        this IServiceCollection services)
    {
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ISecretStore>(_ =>
            new FileSecretStore(SettingsPathResolver.GetSecretsPath()));
        services.AddSingleton<ISettingsPanelFactory, SettingsPanelFactory>();

        return services;
    }
}
