using System;
using System.IO;

namespace Zaide.Tests.Infrastructure;

/// <summary>
/// Shared filesystem helpers for tests: read-only fixture roots and isolated
/// writable temporary directories with guaranteed cleanup.
/// </summary>
public static class TestFilesystem
{
    /// <summary>
    /// Stable read-only workspace root backed by committed fixture content.
    /// Use for tests that only need path strings or read existing fixture files.
    /// </summary>
    public static string SharedReadOnlyWorkspaceRoot => TestFixturePaths.FixturesDirectory;

    /// <summary>
    /// Creates an isolated writable directory deleted on disposal.
    /// </summary>
    public static TestTempDirectory CreateTempDirectory(string prefix) =>
        TestTempDirectory.Create(prefix);
}

/// <summary>
/// Writable temporary directory removed in <see cref="Dispose"/>.
/// </summary>
public sealed class TestTempDirectory : IDisposable
{
    private TestTempDirectory(string path) => Path = path;

    public string Path { get; }

    public static TestTempDirectory Create(string prefix)
    {
        var normalized = prefix.EndsWith("-", StringComparison.Ordinal) ? prefix : prefix + "-";
        var directory = Directory.CreateTempSubdirectory(normalized);
        return new TestTempDirectory(directory.FullName);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch
        {
            // Best-effort cleanup for test isolation.
        }
    }
}
