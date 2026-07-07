using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Services;
using Zaide.ViewModels;

namespace Zaide.Tests.ViewModels;

public class TerminalHostTests
{
    static TerminalHostTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }
    private static readonly Action<Action> RunInline = a => a();

    private static TerminalViewModel CreateViewModel(Mock<ITerminalService> service)
        => new(service.Object, RunInline);

    private static TerminalHost CreateHost(Mock<ITerminalService> service)
    {
        var terminalVm = CreateViewModel(service);
        var factory = new Mock<ITerminalSessionFactory>();
        factory.Setup(f => f.CreateSession()).Returns(terminalVm);
        return new TerminalHost(factory.Object);
    }

    [Fact]
    public void ActiveSession_IsNotNull()
    {
        using var host = CreateHost(new Mock<ITerminalService>());
        Assert.NotNull(host.ActiveSession);
    }

    [Fact]
    public async Task EnsureActiveSessionStartedAsync_DelegatesToSession()
    {
        var service = new Mock<ITerminalService>();
        service.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        service.SetupGet(s => s.IsRunning).Returns(true);

        using var host = CreateHost(service);
        await host.EnsureActiveSessionStartedAsync();

        service.Verify(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Dispose_DisposesActiveSession()
    {
        var serviceDisposed = false;
        var disposableService = new Mock<ITerminalService>();
        disposableService.Setup(s => s.Dispose()).Callback(() => serviceDisposed = true);

        var terminalVm = CreateViewModel(disposableService);
        var factory = new Mock<ITerminalSessionFactory>();
        factory.Setup(f => f.CreateSession()).Returns(terminalVm);

        var host = new TerminalHost(factory.Object);
        host.Dispose();

        Assert.True(serviceDisposed);
    }

    [Fact]
    public async Task StartupError_EmitsOnSessionError()
    {
        var service = new Mock<ITerminalService>();
        service.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new InvalidOperationException("pty failed"));

        using var host = CreateHost(service);

        string? lastError = null;
        using var _ = host.StartupError.Subscribe(err => lastError = err);

        await host.EnsureActiveSessionStartedAsync();

        Assert.Equal("pty failed", lastError);
    }

    [Fact]
    public async Task EnsureActiveSessionStartedAsync_StartsSessionOnlyOnce()
    {
        var service = new Mock<ITerminalService>();
        service.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        service.SetupGet(s => s.IsRunning).Returns(true);

        using var host = CreateHost(service);

        await host.EnsureActiveSessionStartedAsync();
        await host.EnsureActiveSessionStartedAsync(); // second call should be a no-op

        service.Verify(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void TwoHosts_DoNotShareSessionState()
    {
        var service1 = new Mock<ITerminalService>();
        var service2 = new Mock<ITerminalService>();

        using var host1 = CreateHost(service1);
        using var host2 = CreateHost(service2);

        Assert.NotSame(host1.ActiveSession, host2.ActiveSession);
        Assert.NotSame(host1.ActiveSession.LogEntries, host2.ActiveSession.LogEntries);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var host = CreateHost(new Mock<ITerminalService>());
        host.Dispose();
        // Should not throw
        host.Dispose();
    }
}
