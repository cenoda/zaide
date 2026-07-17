using System;
using System.IO;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;

namespace Zaide.Features.Settings.Infrastructure;

/// <summary>
/// Resolves the platform-appropriate settings directory and file paths.
///
/// **Linux:** <c>$XDG_CONFIG_HOME/zaide</c> when <c>XDG_CONFIG_HOME</c> is an
/// absolute path; otherwise <c>$HOME/.config/zaide</c>.
/// **Windows/macOS:** <c>Environment.SpecialFolder.ApplicationData/zaide</c>.
/// </summary>
public static class SettingsPathResolver
{
    /// <summary>Directory containing settings and secret files.</summary>
    public static string GetSettingsDirectory()
    {
        if (OperatingSystem.IsLinux())
        {
            var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (!string.IsNullOrEmpty(xdgConfigHome) && Path.IsPathRooted(xdgConfigHome))
                return Path.Combine(xdgConfigHome, "zaide");

            var home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
                return Path.Combine(home, ".config", "zaide");
        }

        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData))
                return Path.Combine(appData, "zaide");
        }

        // Fallback for unrecognized platforms
        var homeDir = Environment.GetEnvironmentVariable("HOME") ?? "/tmp";
        return Path.Combine(homeDir, ".config", "zaide");
    }

    /// <summary>Full path to <c>settings.json</c>.</summary>
    public static string GetSettingsPath() =>
        Path.Combine(GetSettingsDirectory(), "settings.json");

    /// <summary>Full path to the last-known-good copy.</summary>
    public static string GetLastKnownGoodPath() =>
        Path.Combine(GetSettingsDirectory(), "settings.json.lastknowngood");

    /// <summary>Full path to the temporary file used during atomic writes.</summary>
    public static string GetTempPath() =>
        Path.Combine(GetSettingsDirectory(), "settings.json.tmp");

    /// <summary>Full path to <c>secrets.json</c> (Phase 8.1.2).</summary>
    public static string GetSecretsPath() =>
        Path.Combine(GetSettingsDirectory(), "secrets.json");

    /// <summary>Full path to the temporary file used during secret atomic writes.</summary>
    public static string GetSecretsTempPath() =>
        Path.Combine(GetSettingsDirectory(), "secrets.json.tmp");
}
