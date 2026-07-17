using System.Reactive.Concurrency;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.Builder;
using Xunit;
using Zaide;
using Zaide.Services;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Infrastructure;

namespace Zaide.Tests.Features.ProjectSystem.DI;

/// <summary>
/// Phase 11 M1 DI integration tests for workflow orchestration services.
/// </summary>
public sealed class ProjectWorkflowServiceDiTests
{
    static ProjectWorkflowServiceDiTests()
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
    public void ConfigureServices_ResolvesProjectWorkflowService()
    {
        using var provider = BuildProvider();

        var workflow = provider.GetRequiredService<IProjectWorkflowService>();

        Assert.NotNull(workflow);
        Assert.IsType<ProjectWorkflowService>(workflow);
    }

    [Fact]
    public void ConfigureServices_ResolvesOneSharedWorkflowSingleton()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<IProjectWorkflowService>();
        var second = provider.GetRequiredService<IProjectWorkflowService>();

        Assert.Same(first, second);
    }

    [Fact]
    public void ConfigureServices_ResolvesManagedProcessRunner()
    {
        using var provider = BuildProvider();

        var runner = provider.GetRequiredService<IManagedProcessRunner>();

        Assert.NotNull(runner);
        Assert.IsType<ManagedProcessRunner>(runner);
    }

    [Fact]
    public void ConfigureServices_ResolvesOneSharedManagedProcessRunnerSingleton()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<IManagedProcessRunner>();
        var second = provider.GetRequiredService<IManagedProcessRunner>();

        Assert.Same(first, second);
    }

    [Fact]
    public void ConfigureServices_ResolvesBuildDiagnosticsService()
    {
        using var provider = BuildProvider();

        var service = provider.GetRequiredService<IBuildDiagnosticsService>();

        Assert.NotNull(service);
        Assert.IsType<BuildDiagnosticsService>(service);
    }

    [Fact]
    public void ConfigureServices_ResolvesTestResultsService()
    {
        using var provider = BuildProvider();

        var service = provider.GetRequiredService<ITestResultsService>();

        Assert.NotNull(service);
        Assert.IsType<TestResultsService>(service);
    }
}
