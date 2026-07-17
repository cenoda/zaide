using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.Features.Debugging.Infrastructure.Dap;
using Zaide.ViewModels;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.ProjectSystem.Infrastructure;
using Zaide.Features.Debugging.Application;

namespace Zaide.Tests.Features.Debugging.Application;

/// <summary>
/// Phase 12 M5 production Linux proof: breakpoint stop, stack/frame/scope/variable
/// projection, and continue/stop through the production <see cref="DebugSessionService"/>.
/// </summary>
public sealed class DebugStackProofTests
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
    public async Task ProductionProof_BreakpointStopStackScopeVariableContinueStop()
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
            var start = await debugSession.StartLaunchAsync(new DebugLaunchRequest(
                dll,
                FixtureRoot,
                StopAtEntry: false,
                Breakpoints: new[] { new DebugBreakpointRequest(ProgramSource, 1) }));

            Assert.True(start.Succeeded, start.Message);
            Assert.Equal(DebugSessionState.Stopped, debugSession.Current.State);

            var stackProjection = new DebugStackProjectionViewModel(debugSession);
            stackProjection.Activate();
            await WaitForAsync(() => stackProjection.CallStackState == DebugProjectionState.Ready);

            Assert.NotEmpty(stackProjection.Frames);
            Assert.NotNull(stackProjection.SelectedFrame);
            Assert.Equal(DebugProjectionState.Ready, stackProjection.VariablesState);
            Assert.NotEmpty(stackProjection.Scopes);

            var threads = await debugSession.RequestThreadsAsync();
            Assert.NotNull(threads);
            var threadId = threads!.Value.GetProperty("threads").EnumerateArray().First().GetProperty("id").GetInt32();
            var frameId = stackProjection.SelectedFrame!.Id;
            var scopes = await debugSession.RequestScopesAsync(frameId);
            Assert.NotNull(scopes);
            var variablesReference = scopes!.Value.GetProperty("scopes").EnumerateArray()
                .First()
                .GetProperty("variablesReference")
                .GetInt32();
            var variables = await debugSession.RequestVariablesAsync(variablesReference);
            Assert.NotNull(variables);

            var continueResult = await debugSession.ContinueAsync(threadId);
            Assert.True(continueResult.Succeeded, continueResult.Message);
            await WaitForAsync(() =>
                stackProjection.Frames.Count == 0 ||
                debugSession.Current.State is DebugSessionState.Running or DebugSessionState.Failed);
            Assert.Empty(stackProjection.Frames);

            var stopResult = await debugSession.StopAsync();
            Assert.True(stopResult.Succeeded, stopResult.Message);
            Assert.True(
                debugSession.Current.State is DebugSessionState.Idle or DebugSessionState.Failed,
                $"Unexpected terminal state: {debugSession.Current.State}");
            stackProjection.Dispose();
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

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 10000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (condition())
                return;

            await Task.Delay(50).ConfigureAwait(false);
        }

        throw new TimeoutException("Timed out waiting for M5 production proof state.");
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