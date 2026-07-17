using System.Reactive.Concurrency;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.Builder;
using Xunit;
using Zaide;
using Zaide.Services;
using Zaide.Features.Debugging.Infrastructure.Dap;
using Zaide.ViewModels;
using Zaide.Features.Debugging.Contracts;
using Zaide.Features.Debugging.Application;

namespace Zaide.Tests.Features.Debugging.DI;

/// <summary>
/// Phase 12 M1 DI integration tests for the debug session lifecycle core.
/// </summary>
public sealed class DebugSessionServiceDiTests
{
    static DebugSessionServiceDiTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        Program.ConfigureServices(services);
        services.AddSingleton<IScheduler>(_ => CurrentThreadScheduler.Instance);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void ConfigureServices_ResolvesDebugSessionService()
    {
        using var provider = BuildProvider();

        var session = provider.GetRequiredService<IDebugSessionService>();

        Assert.NotNull(session);
        Assert.IsType<DebugSessionService>(session);
    }

    [Fact]
    public void ConfigureServices_ResolvesOneSharedDebugSessionServiceSingleton()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<IDebugSessionService>();
        var second = provider.GetRequiredService<IDebugSessionService>();

        Assert.Same(first, second);
    }

    [Fact]
    public void ConfigureServices_ResolvesDebugAdapterLocatorAndFactory()
    {
        using var provider = BuildProvider();

        var locator = provider.GetRequiredService<IDebugAdapterLocator>();
        var factory = provider.GetRequiredService<IDebugAdapterSessionFactory>();

        Assert.IsType<DebugAdapterLocator>(locator);
        Assert.IsType<DebugAdapterSessionFactory>(factory);
    }
}
