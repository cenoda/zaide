using System;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Zaide.Services;
using Zaide.ViewModels;

namespace Zaide.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="TerminalViewModel"/>. The UI-marshal seam runs
/// synchronously (<c>a =&gt; a()</c>) so buffer mutations complete inline, and
/// the service is mocked so no real PTY is spawned.
/// </summary>
public class TerminalViewModelTests
{
    private static readonly Action<Action> RunInline = a => a();

    private static TerminalViewModel CreateViewModel(
        Mock<ITerminalService> service, int maxBufferChars = 200_000)
        => new(service.Object, RunInline, maxBufferChars);

    [Fact]
    public void OutputReceived_AppendsToBuffer()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("hello"));

        Assert.Equal("hello", vm.OutputText);
    }

    [Fact]
    public void OutputReceived_DecodesUtf8AcrossChunkBoundaries()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);

        // 'é' (U+00E9) encodes to UTF-8 bytes 0xC3 0xA9 — split across two chunks.
        service.Raise(s => s.OutputReceived += null, new byte[] { 0xC3 });
        Assert.Equal(string.Empty, vm.OutputText); // incomplete sequence, nothing rendered yet

        service.Raise(s => s.OutputReceived += null, new byte[] { 0xA9 });
        Assert.Equal("é", vm.OutputText);
    }

    [Fact]
    public void BufferTrimsWhenFull()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service, maxBufferChars: 10);

        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("0123456789ABCDE"));

        Assert.Equal(10, vm.OutputText.Length);
        Assert.Equal("56789ABCDE", vm.OutputText); // oldest 5 chars trimmed from the front
    }

    [Fact]
    public void ClearCommand_EmptiesBuffer()
    {
        var service = new Mock<ITerminalService>();
        var vm = CreateViewModel(service);
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("hello"));

        vm.ClearCommand.Execute().Subscribe();

        Assert.Equal(string.Empty, vm.OutputText);
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
        Assert.Contains("[Process exited]", vm.OutputText);
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

        Assert.Equal(string.Empty, vm.OutputText);
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
}
