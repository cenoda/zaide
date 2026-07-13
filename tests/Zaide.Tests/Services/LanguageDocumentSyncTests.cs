using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Zaide.Models;
using Zaide.Services;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 10 M2 tests for <see cref="LanguageDocumentBridge"/> LSP document sync.
/// </summary>
public sealed class LanguageDocumentSyncTests
{
    private static readonly string TempRoot = Path.Combine(
        Path.GetTempPath(),
        "zaide-phase10-m2-" + Guid.NewGuid().ToString("N"));

    static LanguageDocumentSyncTests()
    {
        Directory.CreateDirectory(TempRoot);
    }

    // ── Fakes ───────────────────────────────────────────────────────────

    public sealed record DocumentNotification(
        string Method,
        string Uri,
        int? Version,
        string? Text);

    private sealed class RecordingLanguageServerSession : ILanguageServerSession
    {
        public long Generation { get; set; }
        public List<DocumentNotification> Notifications { get; } = new();
        public TaskCompletionSource<bool>? DidOpenGate { get; set; }
        public bool ThrowOnNotify { get; set; }

        public int? ProcessId => 1;
        public bool HasExited { get; private set; }

        public event Action<long>? ProcessExited;

        public Task ShutdownAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ForceKillAsync()
        {
            HasExited = true;
            return Task.CompletedTask;
        }

        public async Task NotifyDidOpenAsync(
            string documentUri,
            int version,
            string text,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ThrowOnNotify)
                throw new InvalidOperationException("notify failed");

            if (DidOpenGate is not null)
                await DidOpenGate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

            Notifications.Add(new DocumentNotification("didOpen", documentUri, version, text));
        }

        public Task NotifyDidChangeAsync(
            string documentUri,
            int version,
            string text,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ThrowOnNotify)
                throw new InvalidOperationException("notify failed");

            Notifications.Add(new DocumentNotification("didChange", documentUri, version, text));
            return Task.CompletedTask;
        }

        public Task NotifyDidCloseAsync(
            string documentUri,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Notifications.Add(new DocumentNotification("didClose", documentUri, null, null));
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void SimulateExit() => ProcessExited?.Invoke(Generation);
    }

    private sealed class FakeLanguageSessionService : ILanguageSessionService
    {
        private readonly Subject<LanguageSessionSnapshot> _subject = new();
        private LanguageSessionSnapshot _current = Unavailable(0);
        private RecordingLanguageServerSession? _session;

        public LanguageSessionSnapshot Current => _current;

        public IObservable<LanguageSessionSnapshot> WhenChanged => _subject;

        public void SetSnapshot(LanguageSessionSnapshot snapshot, RecordingLanguageServerSession? session = null)
        {
            _session = session;
            _current = snapshot;
            _subject.OnNext(snapshot);
        }

        /// <summary>
        /// Replays a delayed snapshot notification without regressing <see cref="Current"/>,
        /// matching production where older generations are never re-published.
        /// </summary>
        public void EmitDelayedSnapshot(LanguageSessionSnapshot snapshot) =>
            _subject.OnNext(snapshot);

        public ILanguageServerSession? TryGetReadySession(long generation) =>
            _current.State == LanguageSessionState.Ready &&
            _current.Generation == generation
                ? _session
                : null;

        public Task RestartAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Dispose()
        {
            _subject.OnCompleted();
            _subject.Dispose();
        }

        private static LanguageSessionSnapshot Unavailable(long generation) => new(
            LanguageSessionState.Unavailable,
            generation,
            ProjectFilePath: null,
            WorkspaceFolderPath: null,
            ServerProcessId: null,
            Failure: null);
    }

    // ── Harness ─────────────────────────────────────────────────────────

    private sealed class TestHarness : IDisposable
    {
        public Workspace Workspace { get; } = new();
        public FakeLanguageSessionService SessionService { get; } = new();
        public RecordingLanguageServerSession ServerSession { get; } = new() { Generation = 1 };
        public LanguageDocumentBridge Bridge { get; }

        public TestHarness()
        {
            Bridge = new LanguageDocumentBridge(
                Workspace,
                SessionService,
                NullLogger<LanguageDocumentBridge>.Instance);
        }

        public void SetReady(long generation = 1)
        {
            ServerSession.Generation = generation;
            SessionService.SetSnapshot(
                new LanguageSessionSnapshot(
                    LanguageSessionState.Ready,
                    generation,
                    "/tmp/project/Project.csproj",
                    "/tmp/project",
                    ServerProcessId: 42,
                    Failure: null),
                ServerSession);
        }

        public void SetState(LanguageSessionState state, long generation)
        {
            SessionService.SetSnapshot(
                new LanguageSessionSnapshot(
                    state,
                    generation,
                    "/tmp/project/Project.csproj",
                    "/tmp/project",
                    ServerProcessId: state == LanguageSessionState.Ready ? 42 : null,
                    Failure: null),
                state == LanguageSessionState.Ready ? ServerSession : null);
        }

        public void Dispose() => Bridge.Dispose();
    }

    private static string CsPath(string name) => Path.Combine(TempRoot, name + ".cs");

    private static async Task WaitForNotificationsAsync(
        RecordingLanguageServerSession session,
        int expectedCount,
        int timeoutMs = 2000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (session.Notifications.Count < expectedCount && Environment.TickCount64 < deadline)
            await Task.Delay(10).ConfigureAwait(false);
    }

    // ── Tests ───────────────────────────────────────────────────────────

    [Fact]
    public async Task OpenEligibleCsWhileReady_SendsSingleDidOpenVersion1()
    {
        using var harness = new TestHarness();
        harness.SetReady();

        var path = CsPath("open");
        var doc = harness.Workspace.OpenDocument(path, "class A {}");

        await WaitForNotificationsAsync(harness.ServerSession, 1);

        var note = Assert.Single(harness.ServerSession.Notifications);
        Assert.Equal("didOpen", note.Method);
        Assert.Equal(LanguageDocumentUri.FromPath(path), note.Uri);
        Assert.Equal(1, note.Version);
        Assert.Equal("class A {}", note.Text);
    }

    [Fact]
    public async Task EditContent_SendsOrderedDidChangeWithMonotonicVersions()
    {
        using var harness = new TestHarness();
        harness.SetReady();

        var path = CsPath("edit");
        var doc = harness.Workspace.OpenDocument(path, "v1");
        await WaitForNotificationsAsync(harness.ServerSession, 1);

        doc.Content = "v2";
        doc.Content = "v3";

        await WaitForNotificationsAsync(harness.ServerSession, 3);

        Assert.Equal(
            new[] { 1, 2, 3 },
            harness.ServerSession.Notifications
                .Where(n => n.Version.HasValue)
                .Select(n => n.Version!.Value)
                .ToArray());

        var changes = harness.ServerSession.Notifications
            .Where(n => n.Method == "didChange")
            .ToArray();
        Assert.Equal(new[] { "v2", "v3" }, changes.Select(n => n.Text).ToArray());
    }

    [Fact]
    public async Task Close_SendsDidCloseAndFurtherEditsDoNotSync()
    {
        using var harness = new TestHarness();
        harness.SetReady();

        var path = CsPath("close");
        var doc = harness.Workspace.OpenDocument(path, "open");
        await WaitForNotificationsAsync(harness.ServerSession, 1);

        harness.Workspace.CloseDocument(path);
        await WaitForNotificationsAsync(harness.ServerSession, 2);

        doc.Content = "after-close";

        await Task.Delay(100);

        Assert.Equal(2, harness.ServerSession.Notifications.Count);
        Assert.Equal("didClose", harness.ServerSession.Notifications[1].Method);
    }

    [Fact]
    public async Task Reopen_StartsNewDidOpenCycleAtVersion1()
    {
        using var harness = new TestHarness();
        harness.SetReady();

        var path = CsPath("reopen");
        var doc = harness.Workspace.OpenDocument(path, "first");
        await WaitForNotificationsAsync(harness.ServerSession, 1);

        harness.Workspace.CloseDocument(path);
        await WaitForNotificationsAsync(harness.ServerSession, 2);

        harness.Workspace.OpenDocument(path, "second");
        await WaitForNotificationsAsync(harness.ServerSession, 3);

        var reopen = harness.ServerSession.Notifications[2];
        Assert.Equal("didOpen", reopen.Method);
        Assert.Equal(1, reopen.Version);
        Assert.Equal("second", reopen.Text);
    }

    [Fact]
    public async Task TabSwitchWithoutContentChange_DoesNotDuplicateDidOpen()
    {
        using var harness = new TestHarness();
        harness.SetReady();

        var path = CsPath("tabswitch");
        var doc = harness.Workspace.OpenDocument(path, "stay");
        await WaitForNotificationsAsync(harness.ServerSession, 1);

        harness.Workspace.OpenDocument(path, "stay");
        harness.Workspace.SetActiveDocument(doc);

        await Task.Delay(100);

        Assert.Single(harness.ServerSession.Notifications);
    }

    [Fact]
    public async Task MultipleDocuments_KeepIndependentVersionSequences()
    {
        using var harness = new TestHarness();
        harness.SetReady();

        var pathA = CsPath("multi-a");
        var pathB = CsPath("multi-b");
        var docA = harness.Workspace.OpenDocument(pathA, "a1");
        var docB = harness.Workspace.OpenDocument(pathB, "b1");
        await WaitForNotificationsAsync(harness.ServerSession, 2);

        docA.Content = "a2";
        docB.Content = "b2";

        await WaitForNotificationsAsync(harness.ServerSession, 4);

        var uriA = LanguageDocumentUri.FromPath(pathA);
        var uriB = LanguageDocumentUri.FromPath(pathB);

        Assert.Equal(1, harness.ServerSession.Notifications.Single(n => n.Uri == uriA && n.Method == "didOpen").Version);
        Assert.Equal(1, harness.ServerSession.Notifications.Single(n => n.Uri == uriB && n.Method == "didOpen").Version);
        Assert.Equal(2, harness.ServerSession.Notifications.Single(n => n.Uri == uriA && n.Method == "didChange").Version);
        Assert.Equal(2, harness.ServerSession.Notifications.Single(n => n.Uri == uriB && n.Method == "didChange").Version);
    }

    [Fact]
    public async Task NonCsFile_DoesNotSync()
    {
        using var harness = new TestHarness();
        harness.SetReady();

        harness.Workspace.OpenDocument(Path.Combine(TempRoot, "readme.md"), "# Title");

        await Task.Delay(100);

        Assert.Empty(harness.ServerSession.Notifications);
    }

    [Fact]
    public async Task EmptyPath_DoesNotSync()
    {
        using var harness = new TestHarness();
        harness.SetReady();

        harness.Workspace.OpenDocument("", "untitled");

        await Task.Delay(100);

        Assert.Empty(harness.ServerSession.Notifications);
    }

    [Fact]
    public async Task SessionNotReady_DoesNotSendUntilReady()
    {
        using var harness = new TestHarness();
        harness.SetState(LanguageSessionState.Unavailable, 1);

        var path = CsPath("not-ready");
        harness.Workspace.OpenDocument(path, "wait");

        await Task.Delay(100);
        Assert.Empty(harness.ServerSession.Notifications);

        harness.SetReady(1);
        await WaitForNotificationsAsync(harness.ServerSession, 1);
    }

    [Fact]
    public async Task SessionBecomesReadyWithOpenDocs_ResyncsDidOpen()
    {
        using var harness = new TestHarness();
        var path = CsPath("resync");
        harness.Workspace.OpenDocument(path, "buffered");

        harness.SetReady(1);
        await WaitForNotificationsAsync(harness.ServerSession, 1);

        Assert.Equal("didOpen", harness.ServerSession.Notifications[0].Method);
        Assert.Equal(1, harness.ServerSession.Notifications[0].Version);
    }

    [Fact]
    public async Task GenerationBump_ResyncsAndIgnoresStaleCallbacks()
    {
        using var harness = new TestHarness();
        harness.SetReady(1);

        var path = CsPath("generation");
        var doc = harness.Workspace.OpenDocument(path, "gen1");
        await WaitForNotificationsAsync(harness.ServerSession, 1);

        harness.ServerSession.DidOpenGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.SetState(LanguageSessionState.Loading, 2);
        doc.Content = "stale-edit";

        harness.ServerSession.DidOpenGate = null;
        harness.SetReady(2);
        await WaitForNotificationsAsync(harness.ServerSession, 2);

        Assert.Equal(2, harness.ServerSession.Notifications.Count(n => n.Method == "didOpen"));
        Assert.DoesNotContain(
            harness.ServerSession.Notifications,
            n => n.Method == "didChange" && n.Text == "stale-edit");
    }

    [Fact]
    public async Task StaleOlderGenerationSnapshot_IsIgnoredAndKeepsNewerSync()
    {
        using var harness = new TestHarness();
        harness.SetReady(1);

        var path = CsPath("out-of-order");
        var doc = harness.Workspace.OpenDocument(path, "initial");
        await WaitForNotificationsAsync(harness.ServerSession, 1);

        doc.Content = "before-bump";
        await WaitForNotificationsAsync(harness.ServerSession, 2);

        var sessionGen1 = harness.ServerSession;
        var gen1NotificationCount = sessionGen1.Notifications.Count;

        var sessionGen2 = new RecordingLanguageServerSession { Generation = 2 };
        sessionGen2.DidOpenGate = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        harness.SetState(LanguageSessionState.Loading, 2);
        harness.SessionService.SetSnapshot(
            new LanguageSessionSnapshot(
                LanguageSessionState.Ready,
                2,
                "/tmp/project/Project.csproj",
                "/tmp/project",
                ServerProcessId: 42,
                Failure: null),
            sessionGen2);

        harness.SessionService.EmitDelayedSnapshot(
            new LanguageSessionSnapshot(
                LanguageSessionState.Ready,
                1,
                "/tmp/project/Project.csproj",
                "/tmp/project",
                ServerProcessId: 42,
                Failure: null));

        sessionGen2.DidOpenGate.SetResult(true);
        await WaitForNotificationsAsync(sessionGen2, 1);

        doc.Content = "after-gen2-ready";
        await WaitForNotificationsAsync(sessionGen2, 2);

        Assert.Equal(gen1NotificationCount, sessionGen1.Notifications.Count);

        Assert.Equal(2, sessionGen2.Notifications.Count);
        Assert.Equal("didOpen", sessionGen2.Notifications[0].Method);
        Assert.Equal("before-bump", sessionGen2.Notifications[0].Text);
        Assert.Equal(1, sessionGen2.Notifications[0].Version);
        Assert.Equal("didChange", sessionGen2.Notifications[1].Method);
        Assert.Equal("after-gen2-ready", sessionGen2.Notifications[1].Text);
        Assert.Equal(2, sessionGen2.Notifications[1].Version);
    }

    [Fact]
    public async Task FailedSessionMidOpen_DoesNotCrashAndDropsFurtherSends()
    {
        using var harness = new TestHarness();
        harness.SetReady(1);

        var path = CsPath("failed");
        var doc = harness.Workspace.OpenDocument(path, "live");
        await WaitForNotificationsAsync(harness.ServerSession, 1);

        harness.SetState(LanguageSessionState.Failed, 2);
        doc.Content = "after-fail";

        await Task.Delay(100);

        Assert.Single(harness.ServerSession.Notifications);
    }

    [Fact]
    public async Task InFlightSyncWork_CancelsOnGenerationChange()
    {
        using var harness = new TestHarness();
        harness.SetReady(1);

        var path = CsPath("cancel");
        harness.Workspace.OpenDocument(path, "block");
        harness.ServerSession.DidOpenGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        harness.SetState(LanguageSessionState.Loading, 2);
        harness.SetReady(2);

        harness.ServerSession.DidOpenGate.SetResult(true);
        await WaitForNotificationsAsync(harness.ServerSession, 1);

        var open = Assert.Single(harness.ServerSession.Notifications);
        Assert.Equal("didOpen", open.Method);
        Assert.Equal(1, open.Version);
    }

    [Fact]
    public async Task InactiveOpenTabs_StaySyncedWithoutActivationReopen()
    {
        using var harness = new TestHarness();
        harness.SetReady();

        var activePath = CsPath("active");
        var inactivePath = CsPath("inactive");
        var active = harness.Workspace.OpenDocument(activePath, "active");
        var inactive = harness.Workspace.OpenDocument(inactivePath, "inactive");
        await WaitForNotificationsAsync(harness.ServerSession, 2);

        harness.Workspace.SetActiveDocument(inactive);
        inactive.Content = "inactive-edit";

        await WaitForNotificationsAsync(harness.ServerSession, 3);

        Assert.Equal(2, harness.ServerSession.Notifications.Count(n => n.Method == "didOpen"));
        Assert.Contains(
            harness.ServerSession.Notifications,
            n => n.Method == "didChange" && n.Uri == LanguageDocumentUri.FromPath(inactivePath));
    }

    [Fact]
    public async Task StaleContentAfterClose_IsIgnored()
    {
        using var harness = new TestHarness();
        harness.SetReady();

        var path = CsPath("stale-close");
        var doc = harness.Workspace.OpenDocument(path, "before");
        await WaitForNotificationsAsync(harness.ServerSession, 1);

        harness.Workspace.CloseDocument(path);
        await WaitForNotificationsAsync(harness.ServerSession, 2);

        doc.Content = "late";

        await Task.Delay(100);

        Assert.Equal(2, harness.ServerSession.Notifications.Count);
    }

    [Fact]
    public async Task MarkCleanWithoutContentChange_DoesNotSendExtraProtocolTraffic()
    {
        using var harness = new TestHarness();
        harness.SetReady();

        var path = CsPath("mark-clean");
        var doc = harness.Workspace.OpenDocument(path, "same");
        await WaitForNotificationsAsync(harness.ServerSession, 1);

        doc.MarkClean();

        await Task.Delay(100);

        Assert.Single(harness.ServerSession.Notifications);
    }

    [Fact]
    public async Task DirtyContentSyncsWithoutSave()
    {
        using var harness = new TestHarness();
        harness.SetReady();

        var path = CsPath("dirty");
        var doc = harness.Workspace.OpenDocument(path, "initial");
        await WaitForNotificationsAsync(harness.ServerSession, 1);

        doc.Content = "unsaved-edit";

        await WaitForNotificationsAsync(harness.ServerSession, 2);

        var change = harness.ServerSession.Notifications.Single(n => n.Method == "didChange");
        Assert.Equal("unsaved-edit", change.Text);
    }
}
