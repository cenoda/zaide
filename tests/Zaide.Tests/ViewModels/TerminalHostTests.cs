using System;
using System.Collections.Generic;
using System.Text;
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

    private static List<Mock<ITerminalService>> CreateHostWithServices(out TerminalHost host)
    {
        var mocks = new List<Mock<ITerminalService>>();
        var factory = new Mock<ITerminalSessionFactory>();
        factory.Setup(f => f.CreateSession()).Returns(() =>
        {
            var mock = new Mock<ITerminalService>();
            mocks.Add(mock);
            return CreateViewModel(mock);
        });
        host = new TerminalHost(factory.Object);
        return mocks;
    }

    // ── M2: Tab Host and Session Lifecycle ──────────────────────────

    [Fact]
    public void NewTerminalTab_CreatesIndependentSession()
    {
        var mocks = CreateHostWithServices(out var host);
        using (host)
        {
            host.NewTabCommand.Execute().Subscribe();
            Assert.Equal(2, host.Tabs.Count);

            // Verify sessions are independent by checking output
            mocks[0].Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("tab1 output"));
            mocks[1].Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("tab2 output"));

        Assert.Contains("tab1 output", GetScreenText(host.Tabs[0].Session));
            Assert.Contains("tab2 output", GetScreenText(host.Tabs[1].Session));
        }
    }

    [Fact]
    public void CloseTerminalTab_DisposesItsSession()
    {
        var serviceDisposed = false;
        var disposableService = new Mock<ITerminalService>();
        disposableService.Setup(s => s.Dispose()).Callback(() => serviceDisposed = true);

        var terminalVm = CreateViewModel(disposableService);
        var factory = new Mock<ITerminalSessionFactory>();
        factory.Setup(f => f.CreateSession()).Returns(terminalVm);

        using var host = new TerminalHost(factory.Object);
        host.NewTabCommand.Execute().Subscribe();

        serviceDisposed = false;
        host.CloseTabCommand.Execute(host.Tabs[0]).Subscribe();
        Assert.True(serviceDisposed);
        Assert.Single(host.Tabs);
    }

    [Fact]
    public void SwitchTerminalTab_PreservesEachSessionSnapshot()
    {
        var mocks = CreateHostWithServices(out var host);
        using (host)
        {
            host.NewTabCommand.Execute().Subscribe();
            Assert.Equal(2, host.Tabs.Count);

            // Write to tab 1 while it's active
            mocks[0].Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("hello tab1"));
            Assert.Contains("hello tab1", GetScreenText(host.Tabs[0].Session));

            // Switch to tab 2
            host.ActivateTabCommand.Execute(host.Tabs[1]).Subscribe();

            // Write to tab 2
            mocks[1].Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("hello tab2"));
            Assert.Contains("hello tab2", GetScreenText(host.Tabs[1].Session));

            // Switch back to tab 1 - state preserved
            host.ActivateTabCommand.Execute(host.Tabs[0]).Subscribe();
            Assert.Contains("hello tab1", GetScreenText(host.Tabs[0].Session));
            Assert.DoesNotContain("hello tab2", GetScreenText(host.Tabs[0].Session));
        }
    }

    [Fact]
    public void CloseActiveTab_FallsBackToNeighbor()
    {
        var mocks = CreateHostWithServices(out var host);
        using (host)
        {
            host.NewTabCommand.Execute().Subscribe();
            host.NewTabCommand.Execute().Subscribe();
            Assert.Equal(3, host.Tabs.Count);

            // Close middle tab (index 1)
            host.CloseTabCommand.Execute(host.Tabs[1]).Subscribe();
            Assert.Equal(2, host.Tabs.Count);
            Assert.Same(host.Tabs[1], host.ActiveTab);
            Assert.True(host.Tabs[1].IsActive);
        }
    }

    [Fact]
    public void CloseLastTab_ResultsInNoActiveTab()
    {
        var mocks = CreateHostWithServices(out var host);
        using (host)
        {
            host.CloseTabCommand.Execute(host.Tabs[0]).Subscribe();
            Assert.Empty(host.Tabs);
            Assert.Null(host.ActiveTab);
        }
    }

    [Fact]
    public void CloseLastTab_ClearsStartupErrorProjection()
    {
        var service = new Mock<ITerminalService>();
        service.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new InvalidOperationException("confirm"));

        var factory = new Mock<ITerminalSessionFactory>();
        factory.Setup(f => f.CreateSession()).Returns(() => CreateViewModel(service));

        using var host = new TerminalHost(factory.Object);
        host.CloseTabCommand.Execute(host.Tabs[0]).Subscribe();

        string? lastError = null;
        using var sub = host.StartupError.Subscribe(err => lastError = err);

        Assert.Empty(host.Tabs);
        Assert.Null(host.ActiveTab);
        Assert.Null(lastError);
    }

    [Fact]
    public async Task ActiveTabErrorProjection_FollowsActiveTab()
    {
        var service1 = new Mock<ITerminalService>();
        var service2 = new Mock<ITerminalService>();
        service1.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("tab1 failed"));
        service2.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("tab2 failed"));

        var factory = new Mock<ITerminalSessionFactory>();
        factory.SetupSequence(f => f.CreateSession())
               .Returns(() => CreateViewModel(service1))
               .Returns(() => CreateViewModel(service2));

        using var host = new TerminalHost(factory.Object);
        host.NewTabCommand.Execute().Subscribe();
        Assert.Equal(2, host.Tabs.Count);

        string? lastError = null;
        using var sub = host.StartupError.Subscribe(err => lastError = err);

        // After NewTab, ActiveTab is the new tab (service2)
        // Start it - should fail with tab2's error
        await host.ActiveTab!.Session.EnsureStartedAsync();
        Assert.Equal("tab2 failed", lastError);

        // Switch to tab 1 - error projection should follow
        host.ActivateTabCommand.Execute(host.Tabs[0]).Subscribe();
        await host.ActiveTab!.Session.EnsureStartedAsync();
        Assert.Equal("tab1 failed", lastError);
    }

    [Fact]
    public void HostDisposal_IsIsolatedFromOtherHosts()
    {
        var mocks1 = CreateHostWithServices(out var host1);
        using (host1)
        {
            var mocks2 = CreateHostWithServices(out var host2);
            using (host2)
            {
                host1.Dispose();

                // host2 sessions should still be alive
                mocks2[0].Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("still alive"));
                Assert.NotNull(host2.ActiveSession!.ScreenSnapshot);
            }
        }
    }

    [Fact]
    public void CloseTab_DoesNotAffectOtherSessions()
    {
        var mocks = CreateHostWithServices(out var host);
        using (host)
        {
            host.NewTabCommand.Execute().Subscribe();

            host.CloseTabCommand.Execute(host.Tabs[0]).Subscribe();

            // Tab 2 (now at index 0) should still work
            mocks[1].Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("survived"));
            Assert.Contains("survived", GetScreenText(host.Tabs[0].Session));
        }
    }

    private static string GetScreenText(TerminalViewModel vm)
    {
        var snap = vm.ScreenSnapshot;
        if (snap is null) return string.Empty;
        return string.Join("\n", snap.Lines).TrimEnd();
    }

    // ── M3: Host-level integration scenarios ────────────────────────

    [Fact]
    public void NewTerminalTab_CreatesDistinctViewModelsAndBothPreserveState()
    {
        var mocks = CreateHostWithServices(out var host);
        using (host)
        {
            host.NewTabCommand.Execute().Subscribe();
            Assert.Equal(2, host.Tabs.Count);

            mocks[0].Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("tab1 output"));
            mocks[1].Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("tab2 output"));

            Assert.Contains("tab1 output", GetScreenText(host.Tabs[0].Session));
            Assert.Contains("tab2 output", GetScreenText(host.Tabs[1].Session));
            Assert.NotSame(host.Tabs[0].Session, host.Tabs[1].Session);
        }
    }

    [Fact]
    public void SwitchTerminalTab_PreservesEachSessionSnapshot_M3()
    {
        var mocks = CreateHostWithServices(out var host);
        using (host)
        {
            host.NewTabCommand.Execute().Subscribe();
            Assert.Equal(2, host.Tabs.Count);

            mocks[0].Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("session_a_state"));
            Assert.Contains("session_a_state", GetScreenText(host.Tabs[0].Session));

            host.ActivateTabCommand.Execute(host.Tabs[1]).Subscribe();
            mocks[1].Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("session_b_state"));
            Assert.Contains("session_b_state", GetScreenText(host.Tabs[1].Session));

            host.ActivateTabCommand.Execute(host.Tabs[0]).Subscribe();
            Assert.Contains("session_a_state", GetScreenText(host.Tabs[0].Session));
            Assert.DoesNotContain("session_b_state", GetScreenText(host.Tabs[0].Session));
        }
    }

    [Fact]
    public void CloseTerminalTab_DisposesOnlyThatSession_M3()
    {
        var serviceDisposed = false;
        var disposableService = new Mock<ITerminalService>();
        disposableService.Setup(s => s.Dispose()).Callback(() => serviceDisposed = true);

        var terminalVm = CreateViewModel(disposableService);
        var factory = new Mock<ITerminalSessionFactory>();
        factory.Setup(f => f.CreateSession()).Returns(terminalVm);

        using var host = new TerminalHost(factory.Object);
        host.NewTabCommand.Execute().Subscribe();
        Assert.Equal(2, host.Tabs.Count);

        serviceDisposed = false;
        host.CloseTabCommand.Execute(host.Tabs[0]).Subscribe();

        Assert.True(serviceDisposed);
        Assert.Single(host.Tabs);
        Assert.NotNull(host.ActiveTab);
    }

    [Fact]
    public void CloseActiveTab_FallsBackToNeighbor_M3()
    {
        var mocks = CreateHostWithServices(out var host);
        using (host)
        {
            host.NewTabCommand.Execute().Subscribe();
            host.NewTabCommand.Execute().Subscribe();
            Assert.Equal(3, host.Tabs.Count);

            var originalActive = host.ActiveTab;
            Assert.Same(host.Tabs[2], originalActive);

            host.CloseTabCommand.Execute(host.Tabs[2]).Subscribe();
            Assert.Equal(2, host.Tabs.Count);
            Assert.NotNull(host.ActiveTab);
            Assert.NotSame(originalActive, host.ActiveTab);

            mocks[1].Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("survived"));
            Assert.Contains("survived", GetScreenText(host.ActiveTab!.Session));
        }
    }

    [Fact]
    public void ToggleBottomPanel_DoesNotDestroyTabSessions_M3()
    {
        var mocks = CreateHostWithServices(out var host);
        using (host)
        {
            host.NewTabCommand.Execute().Subscribe();
            Assert.Equal(2, host.Tabs.Count);

            host.FocusActiveSession();
            host.FocusActiveSession();

            mocks[0].Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes("after toggle"));
            Assert.Contains("after toggle", GetScreenText(host.Tabs[0].Session));
            Assert.Equal(2, host.Tabs.Count);
        }
    }

    [Fact]
    public async Task ActiveSessionError_ReflectsActiveTab_M3()
    {
        var service1 = new Mock<ITerminalService>();
        var service2 = new Mock<ITerminalService>();
        service1.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("tab1 failed"));
        service2.Setup(s => s.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("tab2 failed"));

        var factory = new Mock<ITerminalSessionFactory>();
        factory.SetupSequence(f => f.CreateSession())
               .Returns(() => CreateViewModel(service1))
               .Returns(() => CreateViewModel(service2));

        using var host = new TerminalHost(factory.Object);
        host.NewTabCommand.Execute().Subscribe();
        Assert.Equal(2, host.Tabs.Count);

        string? lastError = null;
        using var sub = host.StartupError.Subscribe(err => lastError = err);

        await host.EnsureActiveSessionStartedAsync();
        Assert.Equal("tab2 failed", lastError);

        host.ActivateTabCommand.Execute(host.Tabs[0]).Subscribe();
        await host.EnsureActiveSessionStartedAsync();
        Assert.Equal("tab1 failed", lastError);
    }
}

