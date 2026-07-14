using System;
using System.IO;

namespace Zaide.Services;

/// <summary>
/// Discovers NetCoreDbg from <c>ZAIDE_NETCOREDBG_PATH</c> or <c>netcoredbg</c> on PATH.
/// Does not bundle, download, or scan well-known directories.
/// </summary>
public sealed class DebugAdapterLocator : IDebugAdapterLocator
{
    public const string UnavailableMessage =
        "NetCoreDbg was not found. Set ZAIDE_NETCOREDBG_PATH or add netcoredbg to PATH.";

    private readonly string? _configuredPath;

    /// <param name="configuredPath">
    /// Optional absolute executable override. When set and the file exists, it wins over PATH discovery.
    /// Production DI passes <c>ZAIDE_NETCOREDBG_PATH</c>.
    /// </param>
    public DebugAdapterLocator(string? configuredPath = null)
    {
        _configuredPath = configuredPath;
    }

    /// <inheritdoc />
    public string? Resolve()
    {
        if (!string.IsNullOrWhiteSpace(_configuredPath) && File.Exists(_configuredPath))
            return Path.GetFullPath(_configuredPath);

        return FindOnPath("netcoredbg");
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
