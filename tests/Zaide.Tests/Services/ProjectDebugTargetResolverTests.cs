using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Services;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 12 M3a tests for <see cref="ProjectDebugTargetResolver"/>.
/// </summary>
public sealed class ProjectDebugTargetResolverTests
{
    private static readonly string TempRoot = Path.Combine(
        Path.GetTempPath(),
        "zaide-phase12-m3a-target-" + Guid.NewGuid().ToString("N"));

    static ProjectDebugTargetResolverTests()
    {
        Directory.CreateDirectory(TempRoot);
    }

    private sealed class FakeManagedProcessRunner : IManagedProcessRunner
    {
        public string? SimulatedStdout { get; set; }
        public int? SimulatedExitCode { get; set; } = 0;
        public bool SimulateStartupFailed { get; set; }
        public ManagedProcessStartRequest? LastRequest { get; private set; }

        public bool IsRunning => false;
        public int? ProcessId => null;
        public event Action<ManagedProcessOutputLine>? OutputReceived;
        public event Action? ProcessStarted;

        public Task<ManagedProcessRunResult> RunAsync(
            ManagedProcessStartRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;

            if (SimulateStartupFailed)
                return Task.FromResult(new ManagedProcessRunResult(null, false, StartupFailed: true));

            if (!string.IsNullOrEmpty(SimulatedStdout))
            {
                OutputReceived?.Invoke(new ManagedProcessOutputLine(
                    request.Generation,
                    ProcessStreamKind.StdOut,
                    SimulatedStdout,
                    DateTimeOffset.UtcNow));
            }

            return Task.FromResult(new ManagedProcessRunResult(
                SimulatedExitCode,
                WasCancelled: false,
                StartupFailed: false));
        }

        public Task KillAsync() => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    [Fact]
    public void ParseTargetPathOutput_AcceptsValidAbsoluteDll()
    {
        var dll = Path.Combine(TempRoot, "App.dll");
        File.WriteAllText(dll, "stub");

        var result = ProjectDebugTargetResolver.ParseTargetPathOutput(dll);

        Assert.True(result.IsSuccess);
        Assert.Equal(Path.GetFullPath(dll), result.TargetPath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n")]
    public void ParseTargetPathOutput_RejectsEmpty(string stdout)
    {
        var result = ProjectDebugTargetResolver.ParseTargetPathOutput(stdout);
        Assert.False(result.IsSuccess);
        Assert.Equal(ProjectDebugTargetResolutionKind.UnsupportedLaunchTarget, result.Kind);
    }

    [Fact]
    public void ParseTargetPathOutput_RejectsRelativePath()
    {
        var result = ProjectDebugTargetResolver.ParseTargetPathOutput("bin/Debug/App.dll");
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ParseTargetPathOutput_RejectsMissingFile()
    {
        var missing = Path.Combine(TempRoot, "missing.dll");
        var result = ProjectDebugTargetResolver.ParseTargetPathOutput(missing);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ParseTargetPathOutput_RejectsNonDll()
    {
        var exe = Path.Combine(TempRoot, "App.exe");
        File.WriteAllText(exe, "stub");

        var result = ProjectDebugTargetResolver.ParseTargetPathOutput(exe);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ParseTargetPathOutput_RejectsMultipleDistinctValues()
    {
        var first = Path.Combine(TempRoot, "first.dll");
        var second = Path.Combine(TempRoot, "second.dll");
        File.WriteAllText(first, "a");
        File.WriteAllText(second, "b");

        var result = ProjectDebugTargetResolver.ParseTargetPathOutput($"{first}\n{second}");
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ResolveTargetPathAsync_UsesMsbuildPropertyQuery_NotBinScan()
    {
        var runner = new FakeManagedProcessRunner();
        var resolver = new ProjectDebugTargetResolver(runner);
        var csproj = Path.Combine(TempRoot, "App.csproj");
        await File.WriteAllTextAsync(csproj, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var dll = Path.Combine(TempRoot, "App.dll");
        File.WriteAllText(dll, "stub");
        runner.SimulatedStdout = dll;

        var result = await resolver.ResolveTargetPathAsync(csproj);

        Assert.True(result.IsSuccess);
        Assert.NotNull(runner.LastRequest);
        Assert.Contains("-getProperty:TargetPath", runner.LastRequest!.Arguments);
        Assert.Contains("msbuild", runner.LastRequest.Arguments);
        Assert.DoesNotContain("bin", runner.LastRequest.Arguments, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveTargetPathAsync_RejectsMissingCsproj()
    {
        var resolver = new ProjectDebugTargetResolver(new FakeManagedProcessRunner());
        var missing = Path.Combine(TempRoot, "Missing.csproj");

        var result = await resolver.ResolveTargetPathAsync(missing);

        Assert.False(result.IsSuccess);
    }
}