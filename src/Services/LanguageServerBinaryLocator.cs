using System;
using System.Collections.Generic;
using System.IO;

namespace Zaide.Services;

/// <summary>
/// Discovers the csharp-ls global tool on PATH or at the conventional
/// <c>~/.dotnet/tools/csharp-ls</c> location. Does not package the server into the app.
/// </summary>
public sealed class LanguageServerBinaryLocator : ILanguageServerBinaryLocator
{
    private readonly string? _configuredPath;

    /// <param name="configuredPath">
    /// Optional absolute path override. When set and the file exists, it wins over PATH discovery.
    /// </param>
    public LanguageServerBinaryLocator(string? configuredPath = null)
    {
        _configuredPath = configuredPath;
    }

    /// <inheritdoc />
    public string? Resolve()
    {
        if (!string.IsNullOrWhiteSpace(_configuredPath) && File.Exists(_configuredPath))
            return Path.GetFullPath(_configuredPath);

        var onPath = FindOnPath("csharp-ls");
        if (onPath is not null)
            return onPath;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dotnetTools = Path.Combine(home, ".dotnet", "tools", "csharp-ls");
        if (File.Exists(dotnetTools))
            return Path.GetFullPath(dotnetTools);

        return null;
    }

    private static string? FindOnPath(string executableName)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
            return null;

        foreach (var segment in pathValue.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = Path.Combine(segment, executableName);
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        return null;
    }
}
