using System;
using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.Features.Settings.Domain;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.ProjectSystem.Infrastructure;
using Zaide.Features.Debugging.Contracts;
using Zaide.Features.Debugging.Application;

namespace Zaide.Tests.Features.Debugging.Application;

/// <summary>
/// Phase 12 M3a production Linux proof: shared gate, workflow build handoff,
/// MSBuild <c>TargetPath</c> resolution, and F5-equivalent launch through
/// <see cref="ProjectDebugLaunchService"/>.
/// </summary>
public sealed class DebugLaunchProofTests
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
    public async Task ProductionHandoff_BuildResolveTargetPathAndLaunch_ThenStop()
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
        var gate = new ProjectOperationGate(debugSession);
        var runner = new ManagedProcessRunner();
        var workflow = new ProjectWorkflowService(
            context,
            gate,
            runner,
            NullLogger<ProjectWorkflowService>.Instance);
        var targetResolver = new ProjectDebugTargetResolver(runner);
        var breakpoints = new EmptyBreakpointService();

        var launch = new ProjectDebugLaunchService(
            context,
            gate,
            workflow,
            targetResolver,
            debugSession,
            breakpoints,
            NullLogger<ProjectDebugLaunchService>.Instance);

        var start = await launch.StartDebuggingAsync();
        var diagnostics = string.Join(" | ", debugSession.Current.DiagnosticOutput);
        Assert.True(
            start.Succeeded,
            $"outcome={start.Outcome}; message={start.Message}; failure={debugSession.Current.Failure?.Message}; diagnostics={diagnostics}");
        Assert.Equal(DebugSessionState.Stopped, debugSession.Current.State);
        Assert.False(gate.IsDebugHandoffActive);

        var dll = debugSession.Current.ProgramPath;
        Assert.NotNull(dll);
        Assert.True(Path.IsPathRooted(dll));
        Assert.Equal(".dll", Path.GetExtension(dll), StringComparer.OrdinalIgnoreCase);
        Assert.True(File.Exists(dll));

        var stop = await debugSession.StopAsync();
        Assert.True(stop.Succeeded, stop.Message);
        Assert.Equal(DebugSessionState.Idle, debugSession.Current.State);

        workflow.Dispose();
        debugSession.Dispose();
        runner.Dispose();
        gate.Dispose();
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

    private sealed class EmptyBreakpointService : IBreakpointService
    {
        public System.Collections.Generic.IReadOnlyList<PersistedBreakpoint> GetBreakpoints() =>
            Array.Empty<PersistedBreakpoint>();

        public Task<BreakpointOperationResult> AddAsync(
            string sourcePath,
            int line,
            System.Threading.CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BreakpointOperationResult> RemoveAsync(
            string sourcePath,
            int line,
            System.Threading.CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BreakpointOperationResult> ToggleAsync(
            string sourcePath,
            int line,
            System.Threading.CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<int>>
            MapToDapReplacementBySource(System.Collections.Generic.IReadOnlyCollection<string> sourcePaths) =>
            new System.Collections.Generic.Dictionary<string, System.Collections.Generic.IReadOnlyList<int>>();
    }
}