using System;
using System.IO;
using Xunit;
using Zaide.Features.Debugging.Infrastructure.Dap;

namespace Zaide.Tests.Features.Debugging.Infrastructure.Dap;

/// <summary>
/// Phase 12 M1 tests for <see cref="DebugAdapterLocator"/> precedence and failure behavior.
/// </summary>
public sealed class DebugAdapterLocatorTests
{
    private static readonly string TempRoot = Path.Combine(
        Path.GetTempPath(),
        "zaide-phase12-m1-locator-" + Guid.NewGuid().ToString("N"));

    static DebugAdapterLocatorTests()
    {
        Directory.CreateDirectory(TempRoot);
    }

    [Fact]
    public void Resolve_ConfiguredPathWins_WhenFileExists()
    {
        var configured = Path.Combine(TempRoot, "configured-netcoredbg");
        File.WriteAllText(configured, string.Empty);

        var locator = new DebugAdapterLocator(configured);

        Assert.Equal(Path.GetFullPath(configured), locator.Resolve());
    }

    [Fact]
    public void Resolve_ConfiguredPathMissing_FallsThroughToPath()
    {
        var onPathDir = Path.Combine(TempRoot, "path-bin");
        Directory.CreateDirectory(onPathDir);
        var onPathExe = Path.Combine(onPathDir, "netcoredbg");
        File.WriteAllText(onPathExe, string.Empty);

        var originalPath = Environment.GetEnvironmentVariable("PATH");
        Environment.SetEnvironmentVariable("PATH", onPathDir);

        try
        {
            var locator = new DebugAdapterLocator(Path.Combine(TempRoot, "missing-netcoredbg"));
            Assert.Equal(Path.GetFullPath(onPathExe), locator.Resolve());
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenNoCandidateExists()
    {
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        Environment.SetEnvironmentVariable("PATH", TempRoot);

        try
        {
            var locator = new DebugAdapterLocator(null);
            Assert.Null(locator.Resolve());
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }

    [Fact]
    public void UnavailableMessage_IsActionable()
    {
        Assert.Contains("ZAIDE_NETCOREDBG_PATH", DebugAdapterLocator.UnavailableMessage);
        Assert.Contains("PATH", DebugAdapterLocator.UnavailableMessage);
    }
}
