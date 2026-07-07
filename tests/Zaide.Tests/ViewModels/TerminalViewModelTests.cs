using System;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Zaide.Services;
using Zaide.ViewModels;

namespace Zaide.Tests.ViewModels;

public class TerminalViewModelTests
{
    private static readonly Action<Action> RunInline = a => a();

    private static TerminalViewModel CreateViewModel(
        Mock<ITerminalService>? service = null)
        => new((service ?? new Mock<ITerminalService>()).Object, RunInline);

    /// <summary>Extracts visible screen text from the snapshot for assertion.</summary>
    private static string GetScreenText(TerminalViewModel vm)
    {
        var snap = vm.ScreenSnapshot;
        if (snap is null) return string.Empty;
        return string.Join("\n", snap.Lines).TrimEnd();
    }

    [Fact]
    public void OutputReceived_AppendsToScreen()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("hello"));

        // "hello" appears in the first line of the screen
        Assert.Equal("hello", vm.ScreenSnapshot!.Lines[0].TrimEnd());
    }

    [Fact]
    public void OutputReceived_DecodesUtf8AcrossChunkBoundaries()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        service.Raise(s => s.OutputReceived += null, new byte[] { 0xC3 });
        Assert.Null(vm.ScreenSnapshot); // incomplete sequence; no snapshot yet

        service.Raise(s => s.OutputReceived += null, new byte[] { 0xA9 });
        Assert.Equal("\u00E9", GetScreenText(vm));
    }

    [Fact]
    public void OutputReceived_DecodesUtf8AcrossChunkBoundaries_SnapshotUpdatedAfterComplete()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        // '\u00E9' encodes to UTF-8 bytes 0xC3 0xA9
        service.Raise(s => s.OutputReceived += null, new byte[] { 0xC3 });
        Assert.Null(vm.ScreenSnapshot);

        service.Raise(s => s.OutputReceived += null, new byte[] { 0xA9 });
        Assert.NotNull(vm.ScreenSnapshot);
        Assert.Equal("\u00E9", GetScreenText(vm));
    }

    [Fact]
    public async Task ClearCommand_SendsCtrlLToRunningTerminal()
    {
        var service = new Mock<ITerminalService>();
        service.SetupGet(s => s.IsRunning).Returns(true);
        service.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        service.Setup(s => s.WriteAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        var vm = CreateViewModel(service);

        await vm.EnsureStartedAsync();
        await vm.ClearCommand.Execute();

        service.Verify(
            s => s.WriteAsync(
                It.Is<byte[]>(data => data.Length == 1 && data[0] == 0x0C),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ClearCommand_ClearsScreen_WhenTerminalIsNotRunning()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("hello"));
        Assert.NotNull(vm.ScreenSnapshot);
        Assert.Contains("hello", GetScreenText(vm));

        await vm.ClearCommand.Execute();

        // Screen is cleared; all cells are spaces
        Assert.Equal(80, vm.ScreenSnapshot!.Columns);
        Assert.Equal(24, vm.ScreenSnapshot.Rows);
        Assert.All(vm.ScreenSnapshot.Cells, cell => Assert.Equal(' ', cell.Char));
    }

    [Fact]
    public async Task ClearCommand_ResetsSgrAttributes_WhenTerminalIsNotRunning()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        // Write styled text so SGR attributes are set (bold + red)
        service.Raise(s => s.OutputReceived += null,
            Encoding.UTF8.GetBytes("\x1B[1;31mred\x1B[0m"));

        await vm.ClearCommand.Execute();

        // After clear, subsequent output should have default attributes
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("X"));
        Assert.NotNull(vm.ScreenSnapshot);
        // The 'X' cell at (0,0) should be default: fg=-1, bg=-1, bold=false
        Assert.Equal(-1, vm.ScreenSnapshot.Cells[0].Foreground);
        Assert.Equal(-1, vm.ScreenSnapshot.Cells[0].Background);
        Assert.False(vm.ScreenSnapshot.Cells[0].Bold);
    }

    [Fact]
    public async Task ProcessExited_UpdatesIsRunning()
    {
        var service = new Mock<ITerminalService>();
        service.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        service.SetupGet(s => s.IsRunning).Returns(true);
        var vm = CreateViewModel(service);

        await vm.EnsureStartedAsync();
        Assert.True(vm.IsRunning);

        service.Raise(s => s.ProcessExited += null);

        Assert.False(vm.IsRunning);
        Assert.Contains("[Process exited]", GetScreenText(vm));
    }

    [Fact]
    public async Task StartupError_SetOnStartFailure()
    {
        var service = new Mock<ITerminalService>();
        service.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new InvalidOperationException("posix_openpt failed"));
        var vm = CreateViewModel(service);

        await vm.EnsureStartedAsync();

        Assert.Equal("posix_openpt failed", vm.StartupError);
        Assert.False(vm.IsRunning);
    }

    [Fact]
    public async Task StartupError_NullOnStartSuccess()
    {
        var service = new Mock<ITerminalService>();
        service.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        service.SetupGet(s => s.IsRunning).Returns(true);
        var vm = CreateViewModel(service);

        await vm.EnsureStartedAsync();

        Assert.Null(vm.StartupError);
        Assert.True(vm.IsRunning);
    }

    [Fact]
    public void Dispose_UnsubscribesAndDisposesService()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        vm.Dispose();
        // Events raised after disposal must not mutate the buffer.
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("late"));

        Assert.Null(vm.ScreenSnapshot);
        service.Verify(s => s.Dispose(), Times.Once);
    }

    [Fact]
    public void Resize_ForwardsToService()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        vm.Resize(120, 30);

        service.Verify(s => s.Resize(120, 30), Times.Once);
    }

    [Fact]
    public void Resize_SkipsWhenDimensionsUnchanged()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        vm.Resize(120, 30);
        vm.Resize(120, 30);

        service.Verify(s => s.Resize(120, 30), Times.Once);
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(-1, 30)]
    [InlineData(120, 0)]
    [InlineData(120, -1)]
    public void Resize_IgnoresInvalidDimensions(int cols, int rows)
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        vm.Resize(cols, rows);

        service.Verify(s => s.Resize(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Resize_ForwardsWhenDimensionsChange()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        vm.Resize(120, 30);
        vm.Resize(100, 25);

        service.Verify(s => s.Resize(120, 30), Times.Once);
        service.Verify(s => s.Resize(100, 25), Times.Once);
    }

    [Fact]
    public async Task Resize_BeforeStart_IsReappliedAfterStartup()
    {
        var service = new Mock<ITerminalService>();
        service.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        service.SetupGet(s => s.IsRunning).Returns(true);
        var vm = CreateViewModel(service);

        vm.Resize(120, 30);
        service.Verify(s => s.Resize(120, 30), Times.Once);

        await vm.EnsureStartedAsync();

        service.Verify(s => s.Resize(120, 30), Times.Exactly(2));
    }

    [Fact]
    public async Task Resize_AfterStart_ForwardsOnce()
    {
        var service = new Mock<ITerminalService>();
        service.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        service.SetupGet(s => s.IsRunning).Returns(true);
        var vm = CreateViewModel(service);

        await vm.EnsureStartedAsync();

        vm.Resize(80, 24);
        service.Verify(s => s.Resize(80, 24), Times.Once);
    }

    // --- M3: state transitions ---

    [Fact]
    public void State_IsNotStarted_BeforeStart()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        Assert.Equal(TerminalState.NotStarted, vm.State);
        Assert.Equal("Not started", vm.StatusLabel);
    }

    [Fact]
    public async Task State_IsRunning_AfterSuccessfulStart()
    {
        var service = new Mock<ITerminalService>();
        service.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        service.SetupGet(s => s.IsRunning).Returns(true);
        var vm = CreateViewModel(service);

        await vm.EnsureStartedAsync();

        Assert.Equal(TerminalState.Running, vm.State);
        Assert.Equal("Running", vm.StatusLabel);
    }

    [Fact]
    public async Task State_IsExited_AfterProcessExit()
    {
        var service = new Mock<ITerminalService>();
        service.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        service.SetupGet(s => s.IsRunning).Returns(true);
        var vm = CreateViewModel(service);

        await vm.EnsureStartedAsync();
        service.Raise(s => s.ProcessExited += null);

        Assert.Equal(TerminalState.Exited, vm.State);
        Assert.Equal("Exited", vm.StatusLabel);
    }

    [Fact]
    public async Task State_IsError_AndLabelIncludesMessage_OnStartFailure()
    {
        var service = new Mock<ITerminalService>();
        service.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new InvalidOperationException("posix_openpt failed"));
        var vm = CreateViewModel(service);

        await vm.EnsureStartedAsync();

        Assert.Equal(TerminalState.Error, vm.State);
        Assert.Equal("Error: posix_openpt failed", vm.StatusLabel);
    }

    // --- M3: ViewModel restart path ---

    [Fact]
    public async Task Restart_StartsServiceAgain_AfterCleanExit()
    {
        var service = new Mock<ITerminalService>();
        service.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        service.SetupGet(s => s.IsRunning).Returns(true);
        var vm = CreateViewModel(service);

        await vm.EnsureStartedAsync();
        service.Raise(s => s.ProcessExited += null);
        Assert.Equal(TerminalState.Exited, vm.State);

        await vm.RestartAsync();

        service.Verify(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        Assert.Equal(TerminalState.Running, vm.State);
    }

    [Fact]
    public async Task Restart_WhileRunning_StopsServiceAndRestartsAfterExit()
    {
        var service = new Mock<ITerminalService>();
        service.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        service.Setup(s => s.StopAsync(It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        service.SetupGet(s => s.IsRunning).Returns(true);
        var vm = CreateViewModel(service);

        await vm.EnsureStartedAsync();
        await vm.RestartAsync();

        service.Verify(s => s.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
        service.Verify(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        service.Raise(s => s.ProcessExited += null);

        service.Verify(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        Assert.Equal(TerminalState.Running, vm.State);
    }

    [Fact]
    public async Task RestartCommand_CanExecute_WhileRunningAndAfterExit()
    {
        var service = new Mock<ITerminalService>();
        service.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        service.SetupGet(s => s.IsRunning).Returns(true);
        var vm = CreateViewModel(service);

        bool canExecute = true;
        using var sub = vm.RestartCommand.CanExecute.Subscribe(v => canExecute = v);

        Assert.True(canExecute);

        await vm.EnsureStartedAsync();
        Assert.True(canExecute);

        service.Raise(s => s.ProcessExited += null);
        Assert.True(canExecute);
    }

    [Fact]
    public async Task Restart_DoesNotDuplicateEventHandling()
    {
        var service = new Mock<ITerminalService>();
        service.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        service.SetupGet(s => s.IsRunning).Returns(true);
        var vm = CreateViewModel(service);

        await vm.EnsureStartedAsync();
        service.Raise(s => s.ProcessExited += null);
        await vm.RestartAsync();

        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("X"));

        // Text should appear exactly once in the screen
        int occurrences = GetScreenText(vm).Count(c => c == 'X');
        Assert.Equal(1, occurrences);
    }

    // --- M4: pipe-lined rendering ---

    [Fact]
    public void Append_ParsesAnsiEscapeSequence()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        // Feed "\x1B[31mred\x1B[0m" which should set red then write "red" then reset
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("\x1B[31mred\x1B[0m"));

        Assert.NotNull(vm.ScreenSnapshot);
        Assert.Equal("red", vm.ScreenSnapshot!.Lines[0].TrimEnd());
        // First cell 'r' should have red foreground (color index 1)
        Assert.Equal(1, vm.ScreenSnapshot.Cells[0].Foreground);
        // After reset, any subsequent writes should have default color
    }

    [Fact]
    public void Append_ParsesClearScreen()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("hello"));
        Assert.NotNull(vm.ScreenSnapshot);
        Assert.Contains("hello", GetScreenText(vm));

        // "\x1B[2J" clears the entire display
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("\x1B[2J"));
        Assert.All(vm.ScreenSnapshot!.Cells, cell => Assert.Equal(' ', cell.Char));
    }

    [Fact]
    public async Task PasteAsync_WhenBracketedPasteDisabled_SendsPlainUtf8()
    {
        var service = new Mock<ITerminalService>();
        service.Setup(s => s.WriteAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        var vm = CreateViewModel(service);

        await vm.PasteAsync("test text");

        service.Verify(
            s => s.WriteAsync(
                It.Is<byte[]>(data => Encoding.UTF8.GetString(data) == "test text"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PasteAsync_WhenBracketedPasteEnabled_WrapsWithBracketedPasteMarkers()
    {
        var service = new Mock<ITerminalService>();
        service.Setup(s => s.WriteAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        var vm = CreateViewModel(service);

        // Enable bracketed paste mode
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("\x1B[?2004h"));

        await vm.PasteAsync("test text");

        service.Verify(
            s => s.WriteAsync(
                It.Is<byte[]>(data => Encoding.UTF8.GetString(data) == "\x1B[200~test text\x1B[201~"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Append_DecSetResetAction_UpdatesBracketedPasteState()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        // Enable bracketed paste mode
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("\x1B[?2004h"));
        Assert.True(vm.IsBracketedPasteEnabled());

        // Disable bracketed paste mode
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("\x1B[?2004l"));
        Assert.False(vm.IsBracketedPasteEnabled());
    }

    [Fact]
    public void CursorPosition_UpdatesAfterWrite()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("AB"));
        Assert.NotNull(vm.ScreenSnapshot);
        Assert.Equal(2, vm.CursorCol);
        Assert.Equal(0, vm.CursorRow);
    }

    [Fact]
    public async Task CursorVisible_TrueWhenRunning()
    {
        var service = new Mock<ITerminalService>();
        service.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        service.SetupGet(s => s.IsRunning).Returns(true);
        var vm = CreateViewModel(service);

        Assert.False(vm.CursorVisible); // not started yet

        await vm.EnsureStartedAsync();
        Assert.True(vm.CursorVisible);
    }

    [Fact]
    public void Resize_UpdatesScreenBuffer()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        vm.Resize(50, 10);
        Assert.NotNull(vm.ScreenSnapshot);
        Assert.Equal(50, vm.ScreenSnapshot!.Columns);
        Assert.Equal(10, vm.ScreenSnapshot.Rows);
    }

    [Fact]
    public void OutputReceived_PopulatesScrollbackInSnapshot()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        vm.Resize(3, 2);
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("abcdefghi"));

        Assert.NotNull(vm.ScreenSnapshot);
        Assert.Equal(2, vm.ScreenSnapshot!.ScrollbackLines.Count);
        Assert.Equal("abc", vm.ScreenSnapshot.ScrollbackLines[0]);
        Assert.Equal("def", vm.ScreenSnapshot.ScrollbackLines[1]);
    }

    // --- M3: Resize and Session Stability ---

    [Fact]
    public async Task Restart_AfterResize_ReappliesLatestViewportSize()
    {
        var service = new Mock<ITerminalService>();
        service.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        var vm = CreateViewModel(service);

        // Start, resize, stop, then restart
        await vm.EnsureStartedAsync();
        vm.Resize(100, 30);
        
        // Simulate process exit
        service.SetupGet(s => s.IsRunning).Returns(false);
        service.Raise(s => s.ProcessExited += null);
        
        await vm.RestartAsync();

        // Should forward the latest dimensions after restart (once during resize, once during restart)
        service.Verify(s => s.Resize(100, 30), Times.Exactly(2));
    }

    [Fact]
    public async Task Restart_RaisesRestartedEvent()
    {
        var service = new Mock<ITerminalService>();
        service.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        var vm = CreateViewModel(service);

        bool eventRaised = false;
        vm.Restarted += () => eventRaised = true;

        // Test the non-running path (simpler to test)
        await vm.RestartAsync();

        // The event should have been raised for the non-running path
        Assert.True(eventRaised);
    }

    [Fact]
    public async Task Restart_WhenRunning_RaisesRestartedEventAfterProcessExit()
    {
        var service = new Mock<ITerminalService>();
        service.SetupGet(s => s.IsRunning).Returns(true);
        service.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        service.Setup(s => s.StopAsync(It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        var vm = CreateViewModel(service);

        await vm.EnsureStartedAsync();
        Assert.True(vm.IsRunning);

        bool eventRaised = false;
        vm.Restarted += () => eventRaised = true;

        await vm.RestartAsync();
        service.Raise(s => s.ProcessExited += null);

        await vm.WaitForRestartCompletionAsync();

        Assert.True(eventRaised, "Restarted event should be raised after running-session restart completes");
    }

    [Fact]
    public void Resize_DuringScrollback_PreservesBufferIntegrity()
    {
        // This test verifies that resize preserves screen buffer integrity
        // by checking that content is maintained and dimensions are updated correctly.
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        // Create initial content
        vm.Resize(50, 10);
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("Line 1\nLine 2\nLine 3\nLine 4\nLine 5\n"));

        // Capture snapshot before resize
        var beforeSnapshot = vm.ScreenSnapshot;
        Assert.NotNull(beforeSnapshot);
        Assert.Contains("Line 1", beforeSnapshot.Lines[0]);
        Assert.Contains("Line 2", beforeSnapshot.Lines[1]);
        Assert.Contains("Line 3", beforeSnapshot.Lines[2]);
        Assert.Contains("Line 4", beforeSnapshot.Lines[3]);
        Assert.Contains("Line 5", beforeSnapshot.Lines[4]);

        // Resize should update dimensions but preserve content integrity
        vm.Resize(30, 5);
        var afterSnapshot = vm.ScreenSnapshot;
        Assert.NotNull(afterSnapshot);
        Assert.Equal(30, afterSnapshot.Columns);
        Assert.Equal(5, afterSnapshot.Rows);
        
        // Verify that content is still present and accessible
        Assert.Contains("Line 1", afterSnapshot.Lines[0]);
        
        // Verify that the screen buffer maintains its internal consistency
        Assert.Equal(afterSnapshot.Columns * afterSnapshot.Rows, afterSnapshot.Cells.Count);
        
        // Verify that cells have reasonable content (not all null/empty)
        int nonEmptyCells = 0;
        foreach (var cell in afterSnapshot.Cells)
        {
            if (cell.Char != '\0')
            {
                nonEmptyCells++;
            }
        }
        Assert.True(nonEmptyCells > 0, "Screen buffer should contain some non-empty cells after resize");
    }

    [Fact]
    public async Task Resize_DuringRunningSession_UpdatesSnapshotDimensionsWithoutCorruption()
    {
        var service = new Mock<ITerminalService>();
        service.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        service.SetupGet(s => s.IsRunning).Returns(true);
        var vm = CreateViewModel(service);

        await vm.EnsureStartedAsync();
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("Initial content\n"));

        // Resize during running session
        vm.Resize(60, 15);

        Assert.NotNull(vm.ScreenSnapshot);
        Assert.Equal(60, vm.ScreenSnapshot!.Columns);
        Assert.Equal(15, vm.ScreenSnapshot.Rows);
    }

    // ── M5: LogEntries behavior ───────────────────────────────────

    [Fact]
    public void OutputReceived_AddsLogEntry()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("hello world\n"));

        Assert.Single(vm.LogEntries);
        Assert.Equal("hello world", vm.LogEntries[0].Content);
        Assert.Equal(LogCategory.Log, vm.LogEntries[0].Category);
        Assert.False(vm.LogEntries[0].HasWarning);
    }

    [Fact]
    public void OutputReceived_MultipleLines_CreatesMultipleLogEntries()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("line1\nline2\nline3\n"));

        Assert.Equal(3, vm.LogEntries.Count);
        Assert.Equal("line1", vm.LogEntries[0].Content);
        Assert.Equal("line2", vm.LogEntries[1].Content);
        Assert.Equal("line3", vm.LogEntries[2].Content);
    }

    [Fact]
    public void OutputReceived_ChunkBoundary_ReassemblesPartialLine()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        // First chunk: partial line without newline
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("hello "));
        Assert.Empty(vm.LogEntries);

        // Second chunk: completes the line with newline
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("world\n"));

        Assert.Single(vm.LogEntries);
        Assert.Equal("hello world", vm.LogEntries[0].Content);
    }

    [Fact]
    public void OutputReceived_ChunkBoundary_MultipleChunks_ReassemblesCorrectly()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("part1 "));
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("part2 "));
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("part3\n"));

        Assert.Single(vm.LogEntries);
        Assert.Equal("part1 part2 part3", vm.LogEntries[0].Content);
    }

    [Fact]
    public void OutputReceived_ChunkBoundary_CompleteLineThenPartial()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        // First chunk: one complete line + start of next line
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("line1\npartial "));
        Assert.Single(vm.LogEntries);
        Assert.Equal("line1", vm.LogEntries[0].Content);

        // Second chunk: completes the partial line
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("rest\n"));

        Assert.Equal(2, vm.LogEntries.Count);
        Assert.Equal("line1", vm.LogEntries[0].Content);
        Assert.Equal("partial rest", vm.LogEntries[1].Content);
    }

    [Fact]
    public void OutputReceived_CategorizesBuildOutput()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("[BUILD] Build succeeded\n"));

        Assert.Single(vm.LogEntries);
        Assert.Equal(LogCategory.Build, vm.LogEntries[0].Category);
    }

    [Fact]
    public void OutputReceived_CategorizesAgentOutput()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("[AGENT] Task completed\n"));

        Assert.Single(vm.LogEntries);
        Assert.Equal(LogCategory.Agent, vm.LogEntries[0].Category);
    }

    [Fact]
    public void OutputReceived_WarningLine_SetsHasWarning()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("warning: something\n"));

        Assert.Single(vm.LogEntries);
        Assert.True(vm.LogEntries[0].HasWarning);
    }

    [Fact]
    public void OutputReceived_ExceptionLine_SetsHasWarning()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("System.Exception: crash\n"));

        Assert.Single(vm.LogEntries);
        Assert.True(vm.LogEntries[0].HasWarning);
    }

    [Fact]
    public void LogEntries_CappedAt1000()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        // Generate 1100 lines of output
        var sb = new StringBuilder();
        for (int i = 0; i < 1100; i++)
        {
            sb.Append($"line{i}\n");
        }
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes(sb.ToString()));

        Assert.Equal(1000, vm.LogEntries.Count);
        // The first 100 lines should have been removed; entry 0 should be "line100"
        Assert.Equal("line100", vm.LogEntries[0].Content);
        Assert.Equal("line1099", vm.LogEntries[^1].Content);
    }

    [Fact]
    public async Task ClearCommand_WhenNotRunning_ClearsLogEntries()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("line1\nline2\nline3\n"));
        Assert.Equal(3, vm.LogEntries.Count);

        await vm.ClearCommand.Execute();

        Assert.Empty(vm.LogEntries);
    }

    [Fact]
    public async Task ClearCommand_WhenRunning_ClearsLogEntries()
    {
        var service = new Mock<ITerminalService>();
        service.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        service.SetupGet(s => s.IsRunning).Returns(true);
        service.Setup(s => s.WriteAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        var vm = CreateViewModel(service);

        await vm.EnsureStartedAsync();
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("line1\nline2\nline3\n"));
        Assert.Equal(3, vm.LogEntries.Count);

        await vm.ClearCommand.Execute();

        // Log entries should be cleared even when the terminal is running
        Assert.Empty(vm.LogEntries);
        // Ctrl+L should still have been sent to the service
        service.Verify(
            s => s.WriteAsync(
                It.Is<byte[]>(data => data.Length == 1 && data[0] == 0x0C),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void IsLogView_DefaultIsFalse()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        Assert.False(vm.IsLogView);
    }

    [Fact]
    public void IsLogView_CanBeToggled()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        vm.IsLogView = true;
        Assert.True(vm.IsLogView);

        vm.IsLogView = false;
        Assert.False(vm.IsLogView);
    }

    [Fact]
    public void LogEntry_HasCorrectTag()
    {
        var build = new LogEntry(1, LogCategory.Build, "build", DateTimeOffset.Now);
        var agent = new LogEntry(2, LogCategory.Agent, "agent", DateTimeOffset.Now);
        var log = new LogEntry(3, LogCategory.Log, "log", DateTimeOffset.Now);

        Assert.Equal("[BUILD]", build.Tag);
        Assert.Equal("[AGENT]", agent.Tag);
        Assert.Equal("[LOG]", log.Tag);
    }

    // ── M3: TUI / alternate-screen integration ────────────────────

    [Fact]
    public void Append_EnterAlternateScreen_SwitchesToCleanSurface()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("prompt$ "));
        Assert.Contains("prompt$", GetScreenText(vm));
        Assert.False(vm.IsAlternateScreenActive);

        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("\x1B[?1049h"));
        Assert.True(vm.IsAlternateScreenActive);

        // The alternate screen is a clean surface; main content is not shown.
        Assert.DoesNotContain("prompt$", GetScreenText(vm));
    }

    [Fact]
    public void Append_EnterThenExitAlternateScreen_RestoresMainSurface()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("prompt$ ls\r\n"));
        Assert.Contains("prompt$ ls", GetScreenText(vm));
        Assert.False(vm.IsAlternateScreenActive);

        // Full-screen app session.
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("\x1B[?1049h"));
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("TUI REDRAW"));
        Assert.True(vm.IsAlternateScreenActive);
        Assert.Contains("TUI REDRAW", GetScreenText(vm));

        // Quit the app — main shell surface returns intact.
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("\x1B[?1049l"));
        Assert.False(vm.IsAlternateScreenActive);
        Assert.Contains("prompt$ ls", GetScreenText(vm));
        Assert.DoesNotContain("TUI REDRAW", GetScreenText(vm));
    }

    [Fact]
    public void Append_LessStylePager_LeavesPromptBackOnScreen()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        // Original shell prompt + history before pager opens.
        service.Raise(s => s.OutputReceived += null,
            Encoding.UTF8.GetBytes("user@host:~$ cat README.md\r\n"));

        // less opens (alternate screen) and draws a few "pages" of redraw noise.
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("\x1B[?1049h"));
        for (int page = 0; page < 5; page++)
        {
            service.Raise(s => s.OutputReceived += null,
                Encoding.UTF8.GetBytes($"\x1B[2J\x1B[HREADME line {page}\x1B[6n"));
        }

        Assert.True(vm.IsAlternateScreenActive);
        Assert.Contains("README line 4", GetScreenText(vm));

        // less quits — the original prompt/history must be restored.
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("\x1B[?1049l"));
        Assert.False(vm.IsAlternateScreenActive);
        Assert.Contains("user@host:~$ cat README.md", GetScreenText(vm));
    }

    [Fact]
    public void Append_RedrawHeavyStatus_DoesNotGetStuckInAlternateScreen()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("dashboard on\r\n"));

        // A redraw-heavy status application alternates whole-screen repaints.
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("\x1B[?1049h"));
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("\x1B[2J\x1B[HCpu: 12%"));
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("\x1B[2J\x1B[HMem: 40%"));
        Assert.True(vm.IsAlternateScreenActive);

        // It exits, as real TUIs do, and the terminal must not be stuck.
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("\x1B[?1049l"));
        Assert.False(vm.IsAlternateScreenActive);
        Assert.Contains("dashboard on", GetScreenText(vm));
    }

    [Fact]
    public void Append_SaveRestoreCursor_RedrawsAtOriginalCell()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        // Write a baseline line, then write text and save the cursor there.
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("ABCDEFGH\r\n"));
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("\x1B[2;1HSAVED\x1B" + "7"));

        // Redraw elsewhere, then restore the cursor and continue writing.
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("\x1B[4;1HOTHER"));
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("\x1B" + "8X"));

        // The trailing write lands back at the saved cell, extending "SAVED".
        Assert.Contains("SAVEDX", GetScreenText(vm));
    }

    [Fact]
    public async Task ShellExit_WhileInAlternateScreen_RestoresMainBuffer()
    {
        var service = new Mock<ITerminalService>();
        service.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        service.SetupGet(s => s.IsRunning).Returns(true);
        var vm = CreateViewModel(service);

        await vm.EnsureStartedAsync();
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("session prompt$ "));

        // A full-screen app is open when the shell dies unexpectedly.
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("\x1B[?1049h"));
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("FULLSCREEN"));
        Assert.True(vm.IsAlternateScreenActive);

        // Process exits while alt-screen active.
        service.SetupGet(s => s.IsRunning).Returns(false);
        service.Raise(s => s.ProcessExited += null);

        Assert.False(vm.IsAlternateScreenActive);
        Assert.Contains("[Process exited]", GetScreenText(vm));
        // The main shell surface is what the user sees, not the TUI.
        Assert.Contains("session prompt$", GetScreenText(vm));
        Assert.DoesNotContain("FULLSCREEN", GetScreenText(vm));
        Assert.Equal(TerminalState.Exited, vm.State);
    }

    [Fact]
    public void Append_OrdinaryShellOutput_StillProjectsCorrectly()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("line one\r\nline two\r\n"));
        Assert.Contains("line one", GetScreenText(vm));
        Assert.Contains("line two", GetScreenText(vm));
        Assert.False(vm.IsAlternateScreenActive);
    }

    [Fact]
    public void Append_NoLogEntries_WhileAlternateScreenActive()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("prompt$ \r\n"));
        Assert.Single(vm.LogEntries);

        // Enter a full-screen app and emit noisy redraw traffic.
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("\x1B[?1049h"));
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("\x1B[2J\x1B[Hnoise line\r\n"));
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("\x1B[2J\x1B[Hmore noise\r\n"));
        Assert.True(vm.IsAlternateScreenActive);

        // No new log entries were produced by the TUI traffic.
        Assert.Single(vm.LogEntries);

        // Exiting the app must not retroactively flush the suppressed partial lines.
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("\x1B[?1049l"));
        Assert.Single(vm.LogEntries);
    }
}
