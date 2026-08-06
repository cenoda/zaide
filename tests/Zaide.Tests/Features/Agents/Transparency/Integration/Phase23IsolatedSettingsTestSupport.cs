using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Infrastructure;

namespace Zaide.Tests.Features.Agents.Transparency.Integration;

internal static class Phase23IsolatedSettingsTestSupport
{
    public static string ConfigureIsolatedSettings(IServiceCollection services)
    {
        var dir = Path.Combine(Path.GetTempPath(), "Phase23IsolatedSettings_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        var settingsPath = Path.Combine(dir, "settings.json");
        var lkgPath = Path.Combine(dir, "settings.last-known-good.json");
        var tempPath = Path.Combine(dir, "settings.tmp");
        var migrator = new SettingsMigrator(new ISettingsMigration[]
        {
            new SettingsMigrationV1ToV2(),
            new SettingsMigrationV2ToV3(),
            new SettingsMigrationV3ToV4(),
        });

        services.RemoveAll<ISettingsService>();
        services.AddSingleton<ISettingsService>(_ =>
            new SettingsService(settingsPath, lkgPath, tempPath, migrator));

        return dir;
    }

    public static void TryDeleteDirectory(string dir)
    {
        if (!Directory.Exists(dir))
        {
            return;
        }

        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
