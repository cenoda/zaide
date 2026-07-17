using System;
using System.IO;
using Xunit;
using Zaide.Services;
using Zaide.Features.Settings.Infrastructure;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Presentation;

namespace Zaide.Tests.Features.Settings.Infrastructure;

/// <summary>
/// Phase 8.1.2 / M2 Linux-specific permission tests for <see cref="FileSecretStore"/>:
/// <c>0600</c> creation, mode retention after rename, and loose-mode repair on load.
/// Skipped on non-Linux platforms.
/// </summary>
public sealed class FileSecretStorePermissionTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _secretsPath;
    private readonly string _tempPath;

    public FileSecretStorePermissionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ZaideSecretPermTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _secretsPath = Path.Combine(_tempDir, "secrets.json");
        _tempPath = Path.Combine(_tempDir, "secrets.json.tmp");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort */ }
    }

    [Fact]
    public void Linux_Set_CreatesFile_WithRestrictiveMode()
    {
        if (!OperatingSystem.IsLinux()) return;

        using var store = new FileSecretStore(_secretsPath, _tempPath);
        store.Set("key", "value");

        Assert.True(File.Exists(_secretsPath));
        var mode = File.GetUnixFileMode(_secretsPath);
        const UnixFileMode expected = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        Assert.Equal(expected, mode);
    }

    [Fact]
    public void Linux_Mode_RetainedAfterRename()
    {
        if (!OperatingSystem.IsLinux()) return;

        using var store = new FileSecretStore(_secretsPath, _tempPath);

        // First write creates the file.
        store.Set("key1", "value1");
        var modeAfterFirst = File.GetUnixFileMode(_secretsPath);

        // Second write creates a new temp and renames over the existing file.
        store.Set("key2", "value2");
        var modeAfterSecond = File.GetUnixFileMode(_secretsPath);

        const UnixFileMode expected = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        Assert.Equal(expected, modeAfterFirst);
        Assert.Equal(expected, modeAfterSecond);
    }

    [Fact]
    public void Linux_LooseMode_RepairedOnLoad()
    {
        if (!OperatingSystem.IsLinux()) return;

        // Pre-create the secrets file with loose permissions (0644).
        File.WriteAllText(_secretsPath, "{}");
        File.SetUnixFileMode(_secretsPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite |
            UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        // Opening the store should repair the mode.
        using var store = new FileSecretStore(_secretsPath, _tempPath);

        var mode = File.GetUnixFileMode(_secretsPath);
        const UnixFileMode expected = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        Assert.Equal(expected, mode);
    }

    [Fact]
    public void Linux_LooseMode_StoreStillFunctional()
    {
        if (!OperatingSystem.IsLinux()) return;

        // Pre-create with loose mode.
        File.WriteAllText(_secretsPath, """{"existing":"data"}""");
        File.SetUnixFileMode(_secretsPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite |
            UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        using var store = new FileSecretStore(_secretsPath, _tempPath);

        // Existing data should still be readable.
        Assert.Equal("data", store.Get("existing"));

        // New writes should work and maintain restrictive mode.
        store.Set("new", "value");
        Assert.Equal("value", store.Get("new"));

        var mode = File.GetUnixFileMode(_secretsPath);
        const UnixFileMode expected = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        Assert.Equal(expected, mode);
    }

    [Fact]
    public void Linux_CorrectMode_NotRepaired()
    {
        if (!OperatingSystem.IsLinux()) return;

        // Create with correct mode via the store itself.
        using (var store = new FileSecretStore(_secretsPath, _tempPath))
        {
            store.Set("key", "value");
        }

        var modeBefore = File.GetUnixFileMode(_secretsPath);
        const UnixFileMode expected = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        Assert.Equal(expected, modeBefore);

        // Re-open — should not change anything.
        using (var store2 = new FileSecretStore(_secretsPath, _tempPath))
        {
            Assert.Equal("value", store2.Get("key"));
        }

        var modeAfter = File.GetUnixFileMode(_secretsPath);
        Assert.Equal(expected, modeAfter);
    }

    [Fact]
    public void Linux_ExternalChmod_RepairedOnSubsequentGet()
    {
        if (!OperatingSystem.IsLinux()) return;

        // Use a single store instance (simulating DI singleton lifetime).
        using var store = new FileSecretStore(_secretsPath, _tempPath);

        // Write a secret — file created with 0600.
        store.Set("apiKey", "secret-value");
        var modeAfterSet = File.GetUnixFileMode(_secretsPath);
        const UnixFileMode expected = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        Assert.Equal(expected, modeAfterSet);

        // Externally chmod to loose mode (0644) after startup.
        File.SetUnixFileMode(_secretsPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite |
            UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        var looseMode = File.GetUnixFileMode(_secretsPath);
        Assert.NotEqual(expected, looseMode); // Confirm it's actually loose

        // Call Get() on the SAME instance — should repair and return value.
        var retrieved = store.Get("apiKey");
        Assert.Equal("secret-value", retrieved);

        // Verify mode was repaired back to 0600.
        var modeAfterGet = File.GetUnixFileMode(_secretsPath);
        Assert.Equal(expected, modeAfterGet);
    }
}
