using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Zaide.Services;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.ProjectSystem.Infrastructure;
using Zaide.Features.Debugging.Application;

namespace Zaide.Tests.Features.Debugging.Application;

/// <summary>
/// Phase 12 M6 production-oriented NetCoreDbg recovery proof (Linux).
/// Fake-adapter-only paths are documented in
/// <c>docs/phases/v2/phase-12/M6_DAP_RECOVERY_PROOF.md</c>.
/// </summary>
public sealed class DebugRecoveryProofTests
{
    private static readonly string FixtureRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures", "workflow-console"));

    private static readonly string ProgramSource = Path.Combine(FixtureRoot, "Program.cs");

    private static readonly string AdapterPath =
        Environment.GetEnvironmentVariable("ZAIDE_NETCOREDBG_PATH")
        ?? "/tmp/zaide-phase12-m0-netcoredbg/netcoredbg/netcoredbg";

    private static bool AdapterAndFixtureAvailable() =>
        File.Exists(AdapterPath) &&
        File.Exists(Path.Combine(FixtureRoot, "WorkflowConsole.csproj"));

    [Fact]
    public async Task ProductionProof_StopRecoverAndRestart_ClearsLiveStateAndAdapterProcess()
    {
        if (!AdapterAndFixtureAvailable())
            return;

        var context = new ProofProjectContextService(FixtureRoot);
        var debugSession = new DebugSessionService(
            context,
            new DebugAdapterLocator(AdapterPath),
            new DebugAdapterSessionFactory(),
            new DebugSessionTimeoutPolicy(),
            NullLogger<DebugSessionService>.Instance);

        using (debugSession)
        {
            var dll = await ResolveTargetDllAsync();
            var launch = new DebugLaunchRequest(
                dll,
                FixtureRoot,
                StopAtEntry: false,
                Breakpoints: new[] { new DebugBreakpointRequest(ProgramSource, 1) });

            var start = await debugSession.StartLaunchAsync(launch);
            Assert.True(start.Succeeded, start.Message);
            Assert.Equal(DebugSessionState.Stopped, debugSession.Current.State);
            var adapterPid = debugSession.Current.AdapterProcessId;
            Assert.NotNull(adapterPid);

            // NetCoreDbg often answers setBreakpoints with verified=false + "pending" before
            // configurationDone; both Pending and Verified are truthful session outcomes.
            Assert.NotEmpty(debugSession.Current.BreakpointVerifications);
            Assert.Contains(
                debugSession.Current.BreakpointVerifications,
                v => v.State is DebugBreakpointVerificationState.Verified
                    or DebugBreakpointVerificationState.Pending);

            var stop = await debugSession.StopAsync();
            Assert.True(stop.Succeeded, stop.Message);
            Assert.Equal(DebugSessionState.Idle, debugSession.Current.State);
            Assert.Null(debugSession.Current.StopInfo);
            Assert.Null(debugSession.Current.AdapterProcessId);
            Assert.Empty(debugSession.Current.BreakpointVerifications);

            // Adapter process must not remain after recovery.
            if (adapterPid is int pid)
            {
                try
                {
                    var process = Process.GetProcessById(pid);
                    // Best effort: process may already be reaped.
                    if (!process.HasExited)
                    {
                        await Task.Delay(500);
                        Assert.True(process.HasExited, $"Adapter process {pid} still running after Stop.");
                    }
                }
                catch (ArgumentException)
                {
                    // Process already gone — expected recovery.
                }
            }

            var restart = await debugSession.StartLaunchAsync(launch);
            Assert.True(restart.Succeeded, restart.Message);
            Assert.Equal(DebugSessionState.Stopped, debugSession.Current.State);

            var finalStop = await debugSession.StopAsync();
            Assert.True(finalStop.Succeeded, finalStop.Message);
            Assert.Equal(DebugSessionState.Idle, debugSession.Current.State);
        }
    }

    [Fact]
    public async Task ProductionProof_MissingAdapter_IsRecoverableFailedState()
    {
        var context = new ProofProjectContextService(FixtureRoot);
        var debugSession = new DebugSessionService(
            context,
            new DebugAdapterLocator("/definitely/missing/netcoredbg-zaide-m6"),
            new DebugAdapterSessionFactory(),
            new DebugSessionTimeoutPolicy(),
            NullLogger<DebugSessionService>.Instance);

        using (debugSession)
        {
            if (!File.Exists(Path.Combine(FixtureRoot, "WorkflowConsole.csproj")))
                return;

            var dll = Path.Combine(FixtureRoot, "bin", "Debug", "net10.0", "WorkflowConsole.dll");
            if (!File.Exists(dll))
            {
                dll = await ResolveTargetDllAsync();
            }

            var result = await debugSession.StartLaunchAsync(new DebugLaunchRequest(
                dll,
                FixtureRoot,
                StopAtEntry: true,
                Breakpoints: Array.Empty<DebugBreakpointRequest>()));

            Assert.False(result.Succeeded);
            Assert.Equal(DebugSessionOutcomeKind.AdapterUnavailable, result.Outcome);
            Assert.Equal(DebugSessionState.Failed, debugSession.Current.State);
            Assert.Contains(
                debugSession.Current.DiagnosticOutput,
                line => line.Contains("[error]"));
        }
    }

    private static async Task<string> ResolveTargetDllAsync()
    {
        var runner = new ManagedProcessRunner();
        using (runner)
        {
            var resolver = new ProjectDebugTargetResolver(runner);
            var csproj = Path.Combine(FixtureRoot, "WorkflowConsole.csproj");
            var resolution = await resolver.ResolveTargetPathAsync(csproj);
            Assert.True(resolution.IsSuccess, resolution.Message);
            return resolution.TargetPath!;
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

        public IObservable<ProjectContext> WhenChanged =>
            System.Reactive.Linq.Observable.Empty<ProjectContext>();

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
