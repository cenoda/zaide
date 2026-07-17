using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;

namespace Zaide.Features.Settings.Infrastructure;

/// <summary>
/// File-backed <see cref="ISecretStore"/> that persists secrets as JSON in a
/// single <c>secrets.json</c> file within the settings directory.
///
/// <para>On Linux, the temporary file used during atomic writes is created with
/// <see cref="FileStreamOptions.UnixCreateMode"/> set to <c>0600</c>
/// (owner read/write only) before any bytes are written. The rename preserves
/// that mode. On every load, an existing file with a non-<c>0600</c> mode is
/// repaired and a warning is logged.</para>
///
/// <para>Windows and macOS retain platform-default ACL behavior.</para>
/// </summary>
public sealed class FileSecretStore : ISecretStore, IDisposable
{
    private readonly string _secretsPath;
    private readonly string _tempPath;
    private readonly object _gate = new();

    /// <summary>
    /// Creates a store backed by the production secrets path
    /// (<see cref="SettingsPathResolver.GetSecretsPath"/>).
    /// </summary>
    public FileSecretStore()
        : this(SettingsPathResolver.GetSecretsPath())
    {
    }

    /// <summary>
    /// Creates a store backed by <paramref name="secretsPath"/>.
    /// The temp path is derived from the same directory.
    /// </summary>
    /// <param name="secretsPath">Absolute path to the secrets JSON file.</param>
    public FileSecretStore(string secretsPath)
    {
        if (string.IsNullOrWhiteSpace(secretsPath))
            throw new ArgumentException("Secrets path must not be empty.", nameof(secretsPath));

        _secretsPath = secretsPath;
        var dir = Path.GetDirectoryName(secretsPath) ?? ".";
        _tempPath = Path.Combine(dir, "secrets.json.tmp");

        RepairPermissionsIfNeeded();
    }

    /// <summary>
    /// Test-only constructor that accepts explicit file paths.
    /// </summary>
    internal FileSecretStore(string secretsPath, string tempPath)
    {
        if (string.IsNullOrWhiteSpace(secretsPath))
            throw new ArgumentException("Secrets path must not be empty.", nameof(secretsPath));
        if (string.IsNullOrWhiteSpace(tempPath))
            throw new ArgumentException("Temp path must not be empty.", nameof(tempPath));

        _secretsPath = secretsPath;
        _tempPath = tempPath;

        RepairPermissionsIfNeeded();
    }

    /// <inheritdoc />
    public string? Get(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_gate)
        {
            var data = Load();
            return data.TryGetValue(key, out var value) ? value : null;
        }
    }

    /// <inheritdoc />
    public void Set(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        lock (_gate)
        {
            var data = Load();
            data[key] = value;
            Save(data);
        }
    }

    /// <inheritdoc />
    public void Delete(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_gate)
        {
            var data = Load();
            if (data.Remove(key))
            {
                Save(data);
            }
        }
    }

    // ── Persistence ────────────────────────────────────────────────────

    private Dictionary<string, string> Load()
    {
        if (!File.Exists(_secretsPath))
            return new Dictionary<string, string>();

        // Repair permissions on every load to catch external chmod after startup
        RepairPermissionsIfNeeded();

        try
        {
            var json = File.ReadAllText(_secretsPath);
            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, string>();

            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new Dictionary<string, string>();
        }
    }

    private void Save(Dictionary<string, string> data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true,
        });

        var dir = Path.GetDirectoryName(_secretsPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        WriteAtomic(_tempPath, _secretsPath, json);
    }

    /// <summary>
    /// Writes <paramref name="content"/> to a same-directory temp file, then
    /// atomically renames it over <paramref name="destination"/>.
    /// On Linux the temp file is created with <c>0600</c> permissions.
    /// </summary>
    private static void WriteAtomic(string tempPath, string destination, string content)
    {
        if (OperatingSystem.IsLinux())
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Options = FileOptions.None,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
            };

            // Remove stale temp if it exists (e.g. from a previous crash).
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            using (var stream = new FileStream(tempPath, options))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(content);
            }
        }
        else
        {
            File.WriteAllText(tempPath, content);
        }

        File.Move(tempPath, destination, overwrite: true);
    }

    // ── Linux permission repair ────────────────────────────────────────

    private void RepairPermissionsIfNeeded()
    {
        if (!OperatingSystem.IsLinux())
            return;

        if (!File.Exists(_secretsPath))
            return;

        try
        {
            var mode = File.GetUnixFileMode(_secretsPath);
            const UnixFileMode expected = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            if ((mode & expected) != expected ||
                (mode & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                         UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
            {
                File.SetUnixFileMode(_secretsPath, expected);
                Console.Error.WriteLine(
                    $"[FileSecretStore] WARNING: repaired permissions on {_secretsPath} " +
                    $"from {mode} to {expected}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[FileSecretStore] WARNING: could not check/repair permissions on {_secretsPath}: {ex.Message}");
        }
    }

    /// <summary>No-op. Present so the store can be used in <c>using</c> statements.</summary>
    public void Dispose() { }
}
