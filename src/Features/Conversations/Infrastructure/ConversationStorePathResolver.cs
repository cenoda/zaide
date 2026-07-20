using System;
using System.IO;
using Zaide.Features.Settings.Infrastructure;

namespace Zaide.Features.Conversations.Infrastructure;

/// <summary>
/// Resolves application-owned conversation persistence paths under the Zaide
/// config directory (sibling layout to <c>settings.json</c>, isolated schema).
/// </summary>
internal static class ConversationStorePathResolver
{
    /// <summary>Directory containing the conversation workspace snapshot file.</summary>
    public static string GetStoreDirectory() =>
        Path.Combine(SettingsPathResolver.GetSettingsDirectory(), "conversations");

    /// <summary>Full path to <c>conversations.json</c>.</summary>
    public static string GetStorePath() =>
        Path.Combine(GetStoreDirectory(), "conversations.json");

    /// <summary>Full path to the last-known-good copy.</summary>
    public static string GetLastKnownGoodPath() =>
        Path.Combine(GetStoreDirectory(), "conversations.json.lastknowngood");

    /// <summary>Full path to the temporary file used during atomic writes.</summary>
    public static string GetTempPath() =>
        Path.Combine(GetStoreDirectory(), "conversations.json.tmp");
}
