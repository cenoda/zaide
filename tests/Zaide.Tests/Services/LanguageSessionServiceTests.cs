using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Zaide.Services;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 10 M1 tests for <see cref="LanguageSessionService"/> lifecycle,
/// eligibility, generation safety, cancellation, and structured failures.
/// </summary>
public sealed class LanguageSessionServiceTests
{
    private static readonly string TempRoot = Path.Combine(
        Path.GetTempPath(),
        "zaide-phase10-m1-" + Guid.NewGuid().ToString("N"));

    static LanguageSessionServiceTests()
    {
        Directory.CreateDirectory(TempRoot);
    }

    // ── Fakes ───────────────────────────────────────────────────────────

    private sealed class FakeProjectContextService : IProjectContextService
    {
        private readonly Subject<ProjectContext> _subject = new();
        private ProjectContext _current = Unloaded();

        public ProjectContext Current => _current;

        public IObservable<ProjectContext> WhenChanged => _subject;

        public void Emit(ProjectContext context)
        {
            _current = context;
            _subject.OnNext(context);
        }

        public Task LoadAsync(string workspaceRoot, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task ReloadAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UnloadAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void SelectProject(ProjectCandidate? candidate) =>
            throw new NotSupportedException();

        public void Dispose()
        {
            _subject.OnCompleted();
            _subject.Dispose();
        }

        private static ProjectContext Unloaded() => new(
            ProjectContextState.Unloaded,
            WorkspaceRoot: null,
            Candidates: Array.Empty<ProjectCandidate>(),
            SelectedProject: null,
            UnsupportedFiles: Array.Empty<string>(),
            ErrorMessage: null);
    }

    private sealed class FakeBinaryLocator : ILanguageServerBinaryLocator
    {
        public string? Path { get; set; } = "/fake/csharp-ls";

        public string? Resolve() => Path;
    }

    private sealed class FakeLanguageServerSession : ILanguageServerSession
    {
        public required long Generation { get; init; }
        public int? ProcessId { get; init; } = 4242;
        public bool HasExited { get; private set; }
        public bool ShutdownCalled { get; private set; }
        public bool ForceKillCalled { get; private set; }
        public bool Disposed { get; private set; }

        public event Action<long>? ProcessExited;
#pragma warning disable CS0067 // Required by ILanguageServerSession; unused in M1 lifecycle fakes.
        public event Action<LanguageServerPublishDiagnostics>? DiagnosticsPublished;
#pragma warning restore CS0067

        public Task ShutdownAsync(CancellationToken cancellationToken)
        {
            ShutdownCalled = true;
            return Task.CompletedTask;
        }

        public Task ForceKillAsync()
        {
            ForceKillCalled = true;
            HasExited = true;
            return Task.CompletedTask;
        }

        public Task NotifyDidOpenAsync(
            string documentUri,
            int version,
            string text,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task NotifyDidChangeAsync(
            string documentUri,
            int version,
            string text,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task NotifyDidCloseAsync(
            string documentUri,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public LanguageServerCapabilities Capabilities => TestLanguageServerSession.DefaultCapabilities;

        public Task<LanguageServerCompletionResult?> RequestCompletionAsync(
            string documentUri,
            int line,
            int character,
            CancellationToken cancellationToken = default) =>
            TestLanguageServerSession.EmptyCompletionAsync(documentUri, line, character, cancellationToken);

        public Task<LanguageServerHoverResult?> RequestHoverAsync(
            string documentUri,
            int line,
            int character,
            CancellationToken cancellationToken = default) =>
            TestLanguageServerSession.EmptyHoverAsync(documentUri, line, character, cancellationToken);

        public Task<LanguageServerDefinitionResult?> RequestDefinitionAsync(
            string documentUri,
            int line,
            int character,
            CancellationToken cancellationToken = default) =>
            TestLanguageServerSession.EmptyDefinitionAsync(documentUri, line, character, cancellationToken);

        public Task<LanguageServerSymbolResult?> RequestDocumentSymbolsAsync(
            string documentUri,
            CancellationToken cancellationToken = default) =>
            TestLanguageServerSession.EmptySymbolsAsync(cancellationToken);

        public Task<LanguageServerSymbolResult?> RequestWorkspaceSymbolsAsync(
            string query,
            CancellationToken cancellationToken = default) =>
            TestLanguageServerSession.EmptySymbolsAsync(cancellationToken);

        public Task<LanguageServerFormattingResult?> RequestFormattingAsync(
            string documentUri,
            CancellationToken cancellationToken = default) =>
            TestLanguageServerSession.EmptyFormattingAsync(cancellationToken);



        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }

        public void SimulateExit()
        {
            if (HasExited)
                return;

            HasExited = true;
            ProcessExited?.Invoke(Generation);
        }
    }

    private sealed class FakeSessionFactory : ILanguageServerSessionFactory
    {
        public List<LanguageServerStartOptions> Starts { get; } = new();
        public List<FakeLanguageServerSession> CreatedSessions { get; } = new();
        public TaskCompletionSource<bool>? StartGate { get; set; }
        public Exception? StartException { get; set; }

        public async Task<ILanguageServerSession> StartAsync(
            LanguageServerStartOptions options,
            CancellationToken cancellationToken)
        {
            Starts.Add(options);

            if (StartGate is not null)
                await StartGate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            if (StartException is not null)
                throw StartException;

            var session = new FakeLanguageServerSession { Generation = options.Generation };
            CreatedSessions.Add(session);
            return session;
        }
    }

    private sealed class SnapshotCollector : IDisposable
    {
        private readonly IDisposable _subscription;
        public List<LanguageSessionSnapshot> Snapshots { get; } = new();

        public SnapshotCollector(ILanguageSessionService service)
        {
            _subscription = service.WhenChanged.Subscribe(s => Snapshots.Add(s));
        }

        public void Dispose() => _subscription.Dispose();
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static ProjectCandidate MakeCandidate(string fileName, ProjectKind kind = ProjectKind.CSharpProject)
    {
        var path = Path.GetFullPath(Path.Combine(TempRoot, fileName));
        return new ProjectCandidate(path, Path.GetFileNameWithoutExtension(path), kind);
    }

    private static ProjectContext MakeContext(
        ProjectContextState state,
        ProjectCandidate? selected,
        IReadOnlyList<ProjectCandidate>? candidates = null)
    {
        var list = candidates ?? (selected is not null ? new[] { selected } : Array.Empty<ProjectCandidate>());
        return new ProjectContext(
            state,
            WorkspaceRoot: TempRoot,
            list,
            selected,
            UnsupportedFiles: Array.Empty<string>(),
            ErrorMessage: state == ProjectContextState.Failed ? "discovery failed" : null);
    }

    private static (LanguageSessionService Service, FakeProjectContextService Context, FakeSessionFactory Factory, FakeBinaryLocator Locator)
        CreateHarness(ProjectContext? initial = null)
    {
        var context = new FakeProjectContextService();
        var factory = new FakeSessionFactory();
        var locator = new FakeBinaryLocator();

        if (initial is not null)
            context.Emit(initial);

        var service = new LanguageSessionService(
            context,
            locator,
            factory,
            NullLogger<LanguageSessionService>.Instance);

        return (service, context, factory, locator);
    }

    private static async Task<LanguageSessionSnapshot> WaitForAsync(
        ILanguageSessionService service,
        Func<LanguageSessionSnapshot, bool> predicate,
        TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(5);
        var deadline = DateTime.UtcNow + timeout.Value;

        while (DateTime.UtcNow < deadline)
        {
            var current = service.Current;
            if (predicate(current))
                return current;

            await Task.Delay(20).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Timed out waiting for language session state. Last={service.Current.State}, gen={service.Current.Generation}");
    }

    // ── Tests ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SingleProject_StartsSession_AndBecomesReady()
    {
        var candidate = MakeCandidate("Single.csproj");
        var (service, context, factory, _) = CreateHarness();
        using var collector = new SnapshotCollector(service);
        using (service)
        {
            context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));

            var ready = await WaitForAsync(service, s => s.State == LanguageSessionState.Ready);

            Assert.Equal(LanguageSessionState.Ready, ready.State);
            Assert.Equal(candidate.FilePath, ready.ProjectFilePath);
            Assert.Equal(Path.GetDirectoryName(candidate.FilePath), ready.WorkspaceFolderPath);
            Assert.NotNull(ready.ServerProcessId);
            Assert.Single(factory.Starts);
            Assert.Equal(candidate.FilePath, factory.Starts[0].ProjectFilePath);
        }
    }

    [Fact]
    public async Task Selected_StartsSession_AndBecomesReady()
    {
        var candidate = MakeCandidate("Selected.sln", ProjectKind.Solution);
        var (service, context, factory, _) = CreateHarness();
        using (service)
        {
            context.Emit(MakeContext(
                ProjectContextState.Selected,
                candidate,
                new[] { candidate, MakeCandidate("Other.csproj") }));

            var ready = await WaitForAsync(service, s => s.State == LanguageSessionState.Ready);

            Assert.Equal(LanguageSessionState.Ready, ready.State);
            Assert.Single(factory.Starts);
            Assert.Equal(candidate.FilePath, factory.Starts[0].ProjectFilePath);
        }
    }

    [Theory]
    [InlineData(ProjectContextState.Unloaded)]
    [InlineData(ProjectContextState.Loading)]
    [InlineData(ProjectContextState.NoProject)]
    [InlineData(ProjectContextState.Unsupported)]
    [InlineData(ProjectContextState.Ambiguous)]
    [InlineData(ProjectContextState.Failed)]
    public async Task IneligibleContext_NeverStartsServer(ProjectContextState state)
    {
        var candidate = MakeCandidate("Ignored.csproj");
        var (service, context, factory, _) = CreateHarness();
        using (service)
        {
            context.Emit(MakeContext(state, state is ProjectContextState.Ambiguous ? null : candidate));

            var snapshot = await WaitForAsync(
                service,
                s => s.Generation > 0 && s.State != LanguageSessionState.Ready);

            Assert.Empty(factory.Starts);
            Assert.NotEqual(LanguageSessionState.Ready, snapshot.State);

            if (state == ProjectContextState.Loading)
                Assert.Equal(LanguageSessionState.Loading, snapshot.State);
            else
                Assert.Equal(LanguageSessionState.Unavailable, snapshot.State);
        }
    }

    [Fact]
    public async Task CancellationDuringRestart_ThrowsAndDoesNotPublishReady()
    {
        var candidate = MakeCandidate("Cancel.csproj");
        var (service, context, factory, _) = CreateHarness(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            await WaitForAsync(service, s => s.State == LanguageSessionState.Ready);

            factory.StartGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var cts = new CancellationTokenSource();
            var restartTask = service.RestartAsync(cts.Token);
            await WaitForAsync(service, s => s.State == LanguageSessionState.Loading && s.Generation > 1);
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await restartTask);
            Assert.False(
                service.Current.State == LanguageSessionState.Ready && service.Current.Generation > 1);
        }
    }

    [Fact]
    public async Task ContextReplacement_TearsDownOldSession_AndStartsNewGenerationWhenEligible()
    {
        var first = MakeCandidate("First.csproj");
        var second = MakeCandidate("Second.csproj");
        var (service, context, factory, _) = CreateHarness(MakeContext(ProjectContextState.SingleProject, first));
        using (service)
        {
            var firstReady = await WaitForAsync(service, s => s.State == LanguageSessionState.Ready);
            var firstSession = factory.CreatedSessions[^1];

            context.Emit(MakeContext(ProjectContextState.Selected, second, new[] { first, second }));

            var secondReady = await WaitForAsync(
                service,
                s => s.State == LanguageSessionState.Ready && s.Generation > firstReady.Generation);

            Assert.True(secondReady.Generation > firstReady.Generation);
            Assert.Equal(2, factory.Starts.Count);
            Assert.True(firstSession.ShutdownCalled || firstSession.ForceKillCalled);
            Assert.True(firstSession.Disposed);
        }
    }

    [Fact]
    public async Task ServerProcessExit_PublishesFailed_AndIncrementsGeneration()
    {
        var candidate = MakeCandidate("Exit.csproj");
        var (service, context, factory, _) = CreateHarness(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            var ready = await WaitForAsync(service, s => s.State == LanguageSessionState.Ready);
            var session = factory.CreatedSessions[^1];

            session.SimulateExit();

            var failed = await WaitForAsync(
                service,
                s => s.State == LanguageSessionState.Failed && s.Generation > ready.Generation);

            Assert.Equal(LanguageSessionFailureKind.ServerExited, failed.Failure?.Kind);
            Assert.True(failed.Generation > ready.Generation);
        }
    }

    [Fact]
    public async Task ExplicitRestart_StartsNewSession_WithHigherGeneration()
    {
        var candidate = MakeCandidate("Restart.csproj");
        var (service, context, factory, _) = CreateHarness(MakeContext(ProjectContextState.SingleProject, candidate));
        using (service)
        {
            var firstReady = await WaitForAsync(service, s => s.State == LanguageSessionState.Ready);

            await service.RestartAsync();

            var secondReady = await WaitForAsync(
                service,
                s => s.State == LanguageSessionState.Ready && s.Generation > firstReady.Generation);

            Assert.Equal(2, factory.Starts.Count);
            Assert.True(secondReady.Generation > firstReady.Generation);
        }
    }

    [Fact]
    public async Task Dispose_TearsDownActiveSession_WithoutLeakedHandles()
    {
        var candidate = MakeCandidate("Dispose.csproj");
        var (service, context, factory, _) = CreateHarness(MakeContext(ProjectContextState.SingleProject, candidate));

        await WaitForAsync(service, s => s.State == LanguageSessionState.Ready);
        var session = factory.CreatedSessions[^1];

        service.Dispose();

        Assert.True(session.ShutdownCalled || session.ForceKillCalled || session.Disposed);
        Assert.True(session.Disposed);
    }

    [Fact]
    public async Task MissingServerBinary_PublishesStructuredFailure_WithoutCrashing()
    {
        var candidate = MakeCandidate("MissingBinary.csproj");
        var (service, context, factory, locator) = CreateHarness();
        locator.Path = null;

        using (service)
        {
            context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));

            var failed = await WaitForAsync(service, s => s.State == LanguageSessionState.Failed);

            Assert.Empty(factory.Starts);
            Assert.Equal(LanguageSessionFailureKind.MissingServerBinary, failed.Failure?.Kind);
            Assert.Contains("csharp-ls", failed.Failure?.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task StaleProcessExit_IsIgnored_AfterContextReplacement()
    {
        var first = MakeCandidate("StaleFirst.csproj");
        var second = MakeCandidate("StaleSecond.csproj");
        var (service, context, factory, _) = CreateHarness(MakeContext(ProjectContextState.SingleProject, first));

        using (service)
        {
            await WaitForAsync(service, s => s.State == LanguageSessionState.Ready);
            var staleSession = factory.CreatedSessions[^1];

            context.Emit(MakeContext(ProjectContextState.SingleProject, second));
            var newReady = await WaitForAsync(
                service,
                s => s.State == LanguageSessionState.Ready && s.ProjectFilePath == second.FilePath);

            staleSession.SimulateExit();
            await Task.Delay(100);

            Assert.Equal(LanguageSessionState.Ready, service.Current.State);
            Assert.Equal(newReady.Generation, service.Current.Generation);
            Assert.Equal(second.FilePath, service.Current.ProjectFilePath);
        }
    }

    [Fact]
    public async Task StartFailure_PublishesStructuredInitializeFailed()
    {
        var candidate = MakeCandidate("InitFail.csproj");
        var (service, context, factory, _) = CreateHarness();
        factory.StartException = new InvalidOperationException("initialize rejected");

        using (service)
        {
            context.Emit(MakeContext(ProjectContextState.SingleProject, candidate));

            var failed = await WaitForAsync(service, s => s.State == LanguageSessionState.Failed);

            Assert.Equal(LanguageSessionFailureKind.InitializeFailed, failed.Failure?.Kind);
            Assert.Equal("initialize rejected", failed.Failure?.Message);
        }
    }
}
