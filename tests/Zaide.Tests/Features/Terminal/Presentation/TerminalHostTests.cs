using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Features.Terminal.Contracts;
using Zaide.Features.Terminal.Presentation;

namespace Zaide.Tests.Features.Terminal.Presentation;

public class TerminalHostTests
{
    static TerminalHostTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    /// <summary>
    /// Production <see cref="TerminalViewModel"/> marshals Append via
    /// <c>Dispatcher.UIThread.Post</c>. Host constructs those VMs itself under
    /// <see cref="ITerminalServiceFactory"/>, so tests cannot inject the
    /// internal RunInline ctor. After each host/tab create, rewrite the private
    /// <c>_uiPost</c> delegate on the actual host-owned instance to run
    /// synchronously so output assertions exercise the real pairing without a
    /// live Avalonia dispatcher pump (which is non-deterministic under xUnit
    /// parallelization).
    /// </summary>
    private static readonly FieldInfo UiPostField =
        typeof(TerminalViewModel).GetField("_uiPost", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("TerminalViewModel._uiPost field not found.");

    private static void InstallSynchronousUiPost(TerminalViewModel session)
    {
        UiPostField.SetValue(session, (Action<Action>)(static action => action()));
    }

    private static void InstallSynchronousUiPost(TerminalHost host)
    {
        foreach (var tab in host.Tabs)
            InstallSynchronousUiPost(tab.Session);
    }

    private static Mock<ITerminalServiceFactory> CreateServiceFactory(
        Func<ITerminalService>? create = null)
    {
        var factory = new Mock<ITerminalServiceFactory>();
        factory.Setup(f => f.Create()).Returns(() =>
            create?.Invoke() ?? new Mock<ITerminalService>().Object);
        return factory;
    }

    private static TerminalHost CreateHost(Mock<ITerminalService> service)
    {
        var factory = CreateServiceFactory(() => service.Object);
        var host = new TerminalHost(factory.Object);
        InstallSynchronousUiPost(host);
        return host;
    }

    private static List<Mock<ITerminalService>> CreateHostWithServices(out TerminalHost host)
    {
        var mocks = new List<Mock<ITerminalService>>();
        var factory = new Mock<ITerminalServiceFactory>();
        factory.Setup(f => f.Create()).Returns(() =>
        {
            var mock = new Mock<ITerminalService>();
            mocks.Add(mock);
            return mock.Object;
        });
        host = new TerminalHost(factory.Object);
        InstallSynchronousUiPost(host);
        return mocks;
    }

    private static void NewTab(TerminalHost host)
    {
        host.NewTabCommand.Execute().Subscribe();
        InstallSynchronousUiPost(host.Tabs[^1].Session);
    }

    private static string GetScreenText(TerminalViewModel vm)
    {
        var snap = vm.ScreenSnapshot;
        if (snap is null) return string.Empty;
        return string.Join("\n", snap.Lines).TrimEnd();
    }

    private static void RaiseOutput(Mock<ITerminalService> service, string text)
    {
        service.Raise(s => s.OutputReceived += null, Encoding.UTF8.GetBytes(text));
    }

    [Fact]
    public void ActiveSession_IsNotNull()
    {
        using var host = CreateHost(new Mock<ITerminalService>());
        Assert.NotNull(host.ActiveSession);
    }

    [Fact]
    public void Constructor_UsesFactoryToCreateInitialService()
    {
        var service = new Mock<ITerminalService>();
        var factory = new Mock<ITerminalServiceFactory>();
        factory.Setup(f => f.Create()).Returns(service.Object);

        using var host = new TerminalHost(factory.Object);
        InstallSynchronousUiPost(host);

        factory.Verify(f => f.Create(), Times.Once);
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

        var factory = CreateServiceFactory(() => disposableService.Object);
        var host = new TerminalHost(factory.Object);
        InstallSynchronousUiPost(host);
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

        Assert.NotSame(host1.ActiveSession!, host2.ActiveSession!);
        Assert.NotSame(host1.ActiveSession!.LogEntries, host2.ActiveSession!.LogEntries);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var host = CreateHost(new Mock<ITerminalService>());
        host.Dispose();
        // Should not throw
        host.Dispose();
    }

    // ── M2: Tab Host and Session Lifecycle ──────────────────────────

    [Fact]
    public void NewTerminalTab_CreatesIndependentSession()
    {
        var mocks = CreateHostWithServices(out var host);
        using (host)
        {
            NewTab(host);
            Assert.Equal(2, host.Tabs.Count);

            // Output through each factory-created service reaches only its paired VM.
            RaiseOutput(mocks[0], "tab1 output");
            RaiseOutput(mocks[1], "tab2 output");

            Assert.Contains("tab1 output", GetScreenText(host.Tabs[0].Session));
            Assert.Contains("tab2 output", GetScreenText(host.Tabs[1].Session));
            Assert.DoesNotContain("tab2 output", GetScreenText(host.Tabs[0].Session));
            Assert.DoesNotContain("tab1 output", GetScreenText(host.Tabs[1].Session));
        }
    }

    [Fact]
    public void NewTab_UsesFactoryForEachService()
    {
        var factory = new Mock<ITerminalServiceFactory>();
        factory.Setup(f => f.Create()).Returns(() => new Mock<ITerminalService>().Object);

        using var host = new TerminalHost(factory.Object);
        InstallSynchronousUiPost(host);
        NewTab(host);

        factory.Verify(f => f.Create(), Times.Exactly(2));
        Assert.Equal(2, host.Tabs.Count);
    }

    [Fact]
    public void CloseTerminalTab_DisposesItsSession()
    {
        var serviceDisposed = false;
        var disposableService = new Mock<ITerminalService>();
        disposableService.Setup(s => s.Dispose()).Callback(() => serviceDisposed = true);

        var call = 0;
        var factory = new Mock<ITerminalServiceFactory>();
        factory.Setup(f => f.Create()).Returns(() =>
        {
            call++;
            return call == 1 ? disposableService.Object : new Mock<ITerminalService>().Object;
        });

        using var host = new TerminalHost(factory.Object);
        InstallSynchronousUiPost(host);
        NewTab(host);

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
            NewTab(host);
            Assert.Equal(2, host.Tabs.Count);

            // Write to tab 1 while it's active
            RaiseOutput(mocks[0], "hello tab1");
            Assert.Contains("hello tab1", GetScreenText(host.Tabs[0].Session));

            // Switch to tab 2
            host.ActivateTabCommand.Execute(host.Tabs[1]).Subscribe();

            // Write to tab 2
            RaiseOutput(mocks[1], "hello tab2");
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
            NewTab(host);
            NewTab(host);
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

        var factory = CreateServiceFactory(() => service.Object);

        using var host = new TerminalHost(factory.Object);
        InstallSynchronousUiPost(host);
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

        var call = 0;
        var factory = new Mock<ITerminalServiceFactory>();
        factory.Setup(f => f.Create()).Returns(() =>
            ++call == 1 ? service1.Object : service2.Object);

        using var host = new TerminalHost(factory.Object);
        InstallSynchronousUiPost(host);
        NewTab(host);
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

                // host2 sessions should still process output after host1 disposal
                RaiseOutput(mocks2[0], "still alive");
                Assert.Contains("still alive", GetScreenText(host2.ActiveSession!));
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
            NewTab(host);

            host.CloseTabCommand.Execute(host.Tabs[0]).Subscribe();

            // Tab 2 (now at index 0) should still process output
            RaiseOutput(mocks[1], "survived");
            Assert.Contains("survived", GetScreenText(host.Tabs[0].Session));
        }
    }

    // ── M3: Host-level integration scenarios ────────────────────────

    [Fact]
    public void NewTerminalTab_CreatesDistinctViewModelsAndBothPreserveState()
    {
        var mocks = CreateHostWithServices(out var host);
        using (host)
        {
            NewTab(host);
            Assert.Equal(2, host.Tabs.Count);

            RaiseOutput(mocks[0], "tab1 output");
            RaiseOutput(mocks[1], "tab2 output");

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
            NewTab(host);
            Assert.Equal(2, host.Tabs.Count);

            RaiseOutput(mocks[0], "session_a_state");
            Assert.Contains("session_a_state", GetScreenText(host.Tabs[0].Session));

            host.ActivateTabCommand.Execute(host.Tabs[1]).Subscribe();
            RaiseOutput(mocks[1], "session_b_state");
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

        var call = 0;
        var factory = new Mock<ITerminalServiceFactory>();
        factory.Setup(f => f.Create()).Returns(() =>
        {
            call++;
            return call == 1 ? disposableService.Object : new Mock<ITerminalService>().Object;
        });

        using var host = new TerminalHost(factory.Object);
        InstallSynchronousUiPost(host);
        NewTab(host);
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
            NewTab(host);
            NewTab(host);
            Assert.Equal(3, host.Tabs.Count);

            var originalActive = host.ActiveTab;
            Assert.Same(host.Tabs[2], originalActive);

            host.CloseTabCommand.Execute(host.Tabs[2]).Subscribe();
            Assert.Equal(2, host.Tabs.Count);
            Assert.NotNull(host.ActiveTab);
            Assert.NotSame(originalActive, host.ActiveTab);

            RaiseOutput(mocks[1], "survived");
            Assert.Contains("survived", GetScreenText(host.ActiveTab!.Session));
        }
    }

    [Fact]
    public void ToggleBottomPanel_DoesNotDestroyTabSessions_M3()
    {
        var mocks = CreateHostWithServices(out var host);
        using (host)
        {
            NewTab(host);
            Assert.Equal(2, host.Tabs.Count);

            host.FocusActiveSession();
            host.FocusActiveSession();

            RaiseOutput(mocks[0], "after toggle");
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

        var call = 0;
        var factory = new Mock<ITerminalServiceFactory>();
        factory.Setup(f => f.Create()).Returns(() =>
            ++call == 1 ? service1.Object : service2.Object);

        using var host = new TerminalHost(factory.Object);
        InstallSynchronousUiPost(host);
        NewTab(host);
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
