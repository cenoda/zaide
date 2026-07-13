using System.Reactive.Concurrency;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.Builder;
using Xunit;
using Zaide;
using Zaide.Services;
using Zaide.ViewModels;

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

    [Fact]
    public void ConfigureServices_ResolvesLanguageDocumentBridge()
    {
        using var provider = BuildProvider();

        var bridge = provider.GetRequiredService<ILanguageDocumentBridge>();

        Assert.NotNull(bridge);
        Assert.IsType<LanguageDocumentBridge>(bridge);
    }

    [Fact]
    public void ConfigureServices_ResolvesLanguageDiagnosticsService()
    {
        using var provider = BuildProvider();

        var diagnostics = provider.GetRequiredService<ILanguageDiagnosticsService>();

        Assert.NotNull(diagnostics);
        Assert.IsType<LanguageDiagnosticsService>(diagnostics);
    }

    [Fact]
    public void ConfigureServices_ResolvesProblemsViewModel()
    {
        using var provider = BuildProvider();

        var problems = provider.GetRequiredService<ProblemsViewModel>();

        Assert.NotNull(problems);
    }
}
