using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Zaide.Services;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 12 M1 production DAP transport lifecycle proof against NetCoreDbg.
/// </summary>
public sealed class NetCoreDbgLifecycleProofTests
{
    private static readonly string FixtureRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures", "workflow-console"));

    private static readonly string AdapterPath =
        Environment.GetEnvironmentVariable("ZAIDE_NETCOREDBG_PATH")
        ?? "/tmp/zaide-phase12-m0-netcoredbg/netcoredbg/netcoredbg";

    private static bool AdapterAndFixtureAvailable() =>
        File.Exists(AdapterPath) &&
        File.Exists(Path.Combine(FixtureRoot, "WorkflowConsole.csproj"));

    [Fact]
    public async Task ProductionSession_RunsFullLinuxLifecycle_ThroughDebugSessionService()
    {
        if (!AdapterAndFixtureAvailable())
            return;

        await BuildFixtureAsync();

        var dllPath = Path.Combine(FixtureRoot, "bin", "Debug", "net10.0", "WorkflowConsole.dll");
        var sourcePath = Path.Combine(FixtureRoot, "Program.cs");
        Assert.True(File.Exists(dllPath), $"Fixture DLL was not built: {dllPath}");

        var locator = new DebugAdapterLocator(AdapterPath);
        var factory = new DebugAdapterSessionFactory();
        var context = new ProofProjectContextService(FixtureRoot);
        using var service = new DebugSessionService(
            context,
            locator,
            factory,
            new DebugSessionTimeoutPolicy(),
            NullLogger<DebugSessionService>.Instance);

        var launch = new DebugLaunchRequest(
            dllPath,
            FixtureRoot,
            StopAtEntry: true,
            new[] { new DebugBreakpointRequest(sourcePath, 1) });

        var start = await service.StartLaunchAsync(launch);
        var diagnostics = string.Join(" | ", service.Current.DiagnosticOutput);
        Assert.True(
            start.Succeeded,
            $"outcome={start.Outcome}; failure={service.Current.Failure?.Message}; diagnostics={diagnostics}");
        Assert.Equal(DebugSessionState.Stopped, service.Current.State);
        Assert.NotNull(service.Current.StopInfo);

        var threads = await service.RequestThreadsAsync();
        Assert.NotNull(threads);
        var threadId = threads!.Value.GetProperty("threads").EnumerateArray().First().GetProperty("id").GetInt32();

        var stack = await service.RequestStackTraceAsync(threadId);
        Assert.NotNull(stack);
        var frameId = stack!.Value.GetProperty("stackFrames").EnumerateArray().First().GetProperty("id").GetInt32();

        var scopes = await service.RequestScopesAsync(frameId);
        Assert.NotNull(scopes);
        Assert.True(scopes!.Value.GetProperty("scopes").GetArrayLength() >= 1);

        var continued = await service.ContinueAsync(threadId);
        Assert.True(continued.Succeeded, continued.Message);

        var stop = await service.StopAsync();
        Assert.True(stop.Succeeded, stop.Message);
        Assert.Equal(DebugSessionState.Idle, service.Current.State);
    }

    private static async Task BuildFixtureAsync()
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{Path.Combine(FixtureRoot, "WorkflowConsole.csproj")}\" --nologo",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start dotnet build for workflow-console fixture.");

        await process.WaitForExitAsync().ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            throw new InvalidOperationException($"Fixture build failed: {stderr}");
        }
    }

    private sealed class ProofProjectContextService : IProjectContextService
    {
        private readonly ProjectContext _current;

        public ProofProjectContextService(string fixtureRoot)
        {
            var csproj = Path.Combine(fixtureRoot, "WorkflowConsole.csproj");
            var candidate = new ProjectCandidate(csproj, "WorkflowConsole", ProjectKind.CSharpProject);
            _current = new ProjectContext(
                ProjectContextState.SingleProject,
                fixtureRoot,
                new[] { candidate },
                candidate,
                UnsupportedFiles: Array.Empty<string>(),
                ErrorMessage: null);
        }

        public ProjectContext Current => _current;

        public IObservable<ProjectContext> WhenChanged => Observable.Empty<ProjectContext>();

        public Task LoadAsync(string workspaceRoot, System.Threading.CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task ReloadAsync(System.Threading.CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UnloadAsync(System.Threading.CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void SelectProject(ProjectCandidate? candidate) =>
            throw new NotSupportedException();

        public void Dispose()
        {
        }
    }
}
