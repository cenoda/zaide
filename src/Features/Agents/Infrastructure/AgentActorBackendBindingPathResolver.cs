using System.IO;
using Zaide.Features.Settings.Infrastructure;

namespace Zaide.Features.Agents.Infrastructure;

/// <summary>
/// Resolves dedicated durable agent/backend binding document paths under the
/// Zaide configuration directory (same roots as settings via
/// <see cref="SettingsPathResolver"/>). Bindings are never written into
/// settings.json, conversation snapshots, or secrets.json.
/// </summary>
internal static class AgentActorBackendBindingPathResolver
{
    public const string PrimaryFileName = "agent-backend-bindings.json";

    public const string TempFileName = "agent-backend-bindings.json.tmp";

    public const string LastKnownGoodFileName = "agent-backend-bindings.json.lastknowngood";

    public static string GetPrimaryPath() =>
        Path.Combine(SettingsPathResolver.GetSettingsDirectory(), PrimaryFileName);

    public static string GetTempPath() =>
        Path.Combine(SettingsPathResolver.GetSettingsDirectory(), TempFileName);

    public static string GetLastKnownGoodPath() =>
        Path.Combine(SettingsPathResolver.GetSettingsDirectory(), LastKnownGoodFileName);

    public static string GetPrimaryPath(string settingsDirectory) =>
        Path.Combine(settingsDirectory, PrimaryFileName);

    public static string GetTempPath(string settingsDirectory) =>
        Path.Combine(settingsDirectory, TempFileName);

    public static string GetLastKnownGoodPath(string settingsDirectory) =>
        Path.Combine(settingsDirectory, LastKnownGoodFileName);
}
