using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Zaide.Services;
using Zaide.Features.Debugging.Infrastructure.Dap;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Infrastructure;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.ProjectSystem.Infrastructure;
using Zaide.Features.Debugging.Contracts;
using Zaide.Features.Debugging.Application;

namespace Zaide.Tests.Features.Debugging.Application;

/// <summary>
/// Phase 12 M3b production Linux proof: persisted breakpoint sent on F5 launch and hit after continue.
/// </summary>
public sealed class DebugBreakpointProofTests
{
    private static readonly string FixtureRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures", "workflow-console"));

    private static readonly string ProgramSource = Path.Combine(FixtureRoot, "Program.cs");

    private static readonly string AdapterPath =
        Environment.GetEnvironmentVariable("ZAIDE_NETCOREDBG_PATH")
        ?? "/tmp/zaide-phase12-m0-netcoredbg/netcoredbg/netcoredbg";

    private static bool AdapterAndFixtureAvailable() =>
        File.Exists(AdapterPath) &&
        File.Exists(Path.Combine(FixtureRoot, "WorkflowConsole.csproj")) &&
        File.Exists(ProgramSource);

    [Fact]
    public async Task ProductionProof_PersistedBreakpointSentAndHitAfterContinue()
    {
        if (!AdapterAndFixtureAvailable())
            return;

        var settingsDir = Path.Combine(Path.GetTempPath(), "zaide-m3b-proof-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(settingsDir);
        var settingsPath = Path.Combine(settingsDir, "settings.json");
        var lastKnownGoodPath = Path.Combine(settingsDir, "settings.json.lastknowngood");
        var tempPath = Path.Combine(settingsDir, "settings.json.tmp");

        using var settings = new SettingsService(
            settingsPath,
            lastKnownGoodPath,
            tempPath,
            new SettingsMigrator(new ISettingsMigration[]
            {
                new SettingsMigrationV1ToV2(),
                new SettingsMigrationV2ToV3(),
            }));

        var context = new ProofProjectContextService(FixtureRoot);
        var breakpoints = new BreakpointService(context, settings);
        var normalizedSource = Path.GetFullPath(ProgramSource);
        var add = await breakpoints.AddAsync(normalizedSource, 2);
        Assert.True(add.Succeeded, add.Message);

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
        var launch = new ProjectDebugLaunchService(
            context,
            gate,
            workflow,
            targetResolver,
            debugSession,
            breakpoints,
            NullLogger<ProjectDebugLaunchService>.Instance);

        try
        {
            var start = await launch.StartDebuggingAsync();
            Assert.True(start.Succeeded, start.Message);
            Assert.Equal(DebugSessionState.Stopped, debugSession.Current.State);
            Assert.Equal("entry", debugSession.Current.StopInfo?.Reason);

            var threadId = debugSession.Current.StopInfo?.ThreadId;
            Assert.NotNull(threadId);

            var continueResult = await debugSession.ContinueAsync(threadId.Value);
            Assert.True(continueResult.Succeeded, continueResult.Message);

            var breakpointStop = await WaitForAsync(
                debugSession,
                snapshot => snapshot.State == DebugSessionState.Stopped &&
                            string.Equals(snapshot.StopInfo?.Reason, "breakpoint", StringComparison.Ordinal),
                TimeSpan.FromSeconds(15));

            Assert.Equal("breakpoint", breakpointStop.StopInfo?.Reason);
            Assert.NotNull(breakpointStop.StopInfo?.ThreadId);

            var stop = await debugSession.StopAsync();
            Assert.True(stop.Succeeded, stop.Message);
            Assert.Equal(DebugSessionState.Idle, debugSession.Current.State);
        }
        finally
        {
            workflow.Dispose();
            debugSession.Dispose();
            runner.Dispose();
            gate.Dispose();

            try { Directory.Delete(settingsDir, recursive: true); }
            catch { /* best-effort */ }
        }
    }

    private static async Task<DebugSessionSnapshot> WaitForAsync(
        IDebugSessionService service,
        Func<DebugSessionSnapshot, bool> predicate,
        TimeSpan timeout)
    {
        var snapshots = new List<string> { DescribeSnapshot(service.Current) };
        using var subscription = service.WhenChanged.Subscribe(snapshot =>
        {
            lock (snapshots)
                snapshots.Add(DescribeSnapshot(snapshot));
        });

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate(service.Current))
                return service.Current;

            await Task.Delay(50);
        }

        string sequence;
        lock (snapshots)
            sequence = string.Join(" -> ", snapshots);

        throw new TimeoutException(
            $"Timed out waiting for debug session predicate. state={service.Current.State}; " +
            $"reason={service.Current.StopInfo?.Reason}; snapshots={sequence}");
    }

    private static string DescribeSnapshot(DebugSessionSnapshot snapshot)
    {
        var verification = string.Join(
            ",",
            snapshot.BreakpointVerifications.Select(item =>
                $"{item.RequestedLine}>{item.ActualLine}:{item.State}"));
        return $"{DateTime.UtcNow:O}[{snapshot.State};reason={snapshot.StopInfo?.Reason};" +
               $"breakpoints={verification};diagnostics={snapshot.DiagnosticOutput.Count}]";
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
