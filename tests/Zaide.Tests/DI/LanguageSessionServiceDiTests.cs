using System.Reactive.Concurrency;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.Builder;
using Xunit;
using Zaide;
using Zaide.Services;

namespace Zaide.Tests.DI;

/// <summary>
/// Phase 10 M1 DI integration tests for <see cref="ILanguageSessionService"/>.
/// </summary>
public sealed class LanguageSessionServiceDiTests
{
    static LanguageSessionServiceDiTests()
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
    public void ConfigureServices_ResolvesLanguageSessionService()
    {
        using var provider = BuildProvider();

        var session = provider.GetRequiredService<ILanguageSessionService>();

        Assert.NotNull(session);
        Assert.IsType<LanguageSessionService>(session);
    }

    [Fact]
    public void ConfigureServices_ResolvesOneSharedLanguageSessionServiceSingleton()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<ILanguageSessionService>();
        var second = provider.GetRequiredService<ILanguageSessionService>();

        Assert.Same(first, second);
    }
}
