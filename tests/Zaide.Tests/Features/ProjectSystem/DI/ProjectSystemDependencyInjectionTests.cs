using System.Reactive.Concurrency;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.Builder;
using Xunit;
using Zaide;
using Zaide.App.Composition;
using Zaide.App.Shell;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.ProjectSystem.Contracts;

namespace Zaide.Tests.Features.ProjectSystem.DI;

/// <summary>
/// Phase 8.3 M3 dependency-injection integration tests. Verifies the production
/// registrations in <see cref="Program.ConfigureServices"/> expose exactly one
/// shared <see cref="IProjectContextService"/> singleton backed by a shared
/// <see cref="IProjectDiscovery"/>, and that <see cref="MainWindowViewModel"/>
/// resolves with the new required <see cref="IProjectContextService"/> dependency.
/// </summary>
public sealed class ProjectSystemDependencyInjectionTests
{
    static ProjectSystemDependencyInjectionTests()
    {
        // ReactiveUI must be initialized before resolving ViewModels that use
        // WhenAnyValue/RaiseAndSetIfChanged in their constructors.
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    /// <summary>
    /// Builds the production container, substituting only the Avalonia scheduler
    /// with a test-safe one so resolution does not require a running UI host.
    /// </summary>
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        Program.ConfigureServices(services);
        services.AddSingleton<IScheduler>(_ => CurrentThreadScheduler.Instance);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void ConfigureServices_ResolvesOneSharedProjectContextServiceSingleton()
    {
        using var provider = BuildProvider();

        var ctx1 = provider.GetRequiredService<IProjectContextService>();
        var ctx2 = provider.GetRequiredService<IProjectContextService>();

        Assert.NotNull(ctx1);
        Assert.Same(ctx1, ctx2);
    }

    [Fact]
    public void ConfigureServices_ResolvesOneSharedDiscoveryDependency()
    {
        using var provider = BuildProvider();

        var discovery1 = provider.GetRequiredService<IProjectDiscovery>();
        var discovery2 = provider.GetRequiredService<IProjectDiscovery>();

        Assert.NotNull(discovery1);
        Assert.Same(discovery1, discovery2);
    }

    [Fact]
    public void ConfigureServices_RegistersProjectContextServiceAgainstWorkspace()
    {
        using var provider = BuildProvider();

        // The service depends on the single shared global::Zaide.Features.Workspace.Domain.Workspace instance.
        var workspace = provider.GetRequiredService<global::Zaide.Features.Workspace.Domain.Workspace>();
        var ctx = provider.GetRequiredService<IProjectContextService>();

        Assert.NotNull(workspace);
        Assert.NotNull(ctx);
    }

    [Fact]
    public void MainWindowViewModel_ResolvesWithRequiredProjectContextService()
    {
        using var provider = BuildProvider();

        var vm = provider.GetRequiredService<MainWindowViewModel>();

        Assert.NotNull(vm);
        // The resolved singleton is shared across the container.
        Assert.Same(
            provider.GetRequiredService<IProjectContextService>(),
            provider.GetRequiredService<IProjectContextService>());
    }
}
