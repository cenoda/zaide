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
    public void ClearCommand_ClearsScreen()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("hello"));
        Assert.NotNull(vm.ScreenSnapshot);
        Assert.Contains("hello", GetScreenText(vm));

        vm.ClearCommand.Execute().Subscribe();

        // Screen is cleared; all cells are spaces
        Assert.Equal(80, vm.ScreenSnapshot!.Columns);
        Assert.Equal(24, vm.ScreenSnapshot.Rows);
        Assert.All(vm.ScreenSnapshot.Cells, cell => Assert.Equal(' ', cell.Char));
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
    public async Task Restart_IsNoOp_WhileRunning()
    {
        var service = new Mock<ITerminalService>();
        service.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        service.SetupGet(s => s.IsRunning).Returns(true);
        var vm = CreateViewModel(service);

        await vm.EnsureStartedAsync();
        await vm.RestartAsync();

        service.Verify(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RestartCommand_CanExecute_OnlyWhenNotRunning()
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
        Assert.False(canExecute);

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
}
