using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Zaide.Models;
using Zaide.Services;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 12 M4 production Linux proof: launch, breakpoint stop, step, and stop
/// through the production <see cref="DebugSessionService"/>.
/// </summary>
public sealed class M4DebugExecutionProofTests
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
    public async Task ProductionProof_LaunchBreakpointStepAndStop()
    {
        if (!AdapterAndFixtureAvailable())
        {
            throw new InvalidOperationException(
                $"M4 execution proof prerequisites are missing. Adapter='{AdapterPath}', fixture='{FixtureRoot}'.");
        }

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
            var start = await debugSession.StartLaunchAsync(new DebugLaunchRequest(
                dll,
                FixtureRoot,
                StopAtEntry: false,
                Breakpoints: new[] { new DebugBreakpointRequest(ProgramSource, 1) }));

            Assert.True(start.Succeeded, start.Message);
            Assert.Equal(DebugSessionState.Stopped, debugSession.Current.State);
            Assert.Equal("breakpoint", debugSession.Current.StopInfo?.Reason);

            var stepResult = await debugSession.StepOverAsync();
            Assert.True(stepResult.Succeeded, stepResult.Message);

            var stopResult = await debugSession.StopAsync();
            Assert.True(stopResult.Succeeded, stopResult.Message);
            Assert.Equal(DebugSessionState.Idle, debugSession.Current.State);
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

        public IObservable<ProjectContext> WhenChanged => System.Reactive.Linq.Observable.Empty<ProjectContext>();

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