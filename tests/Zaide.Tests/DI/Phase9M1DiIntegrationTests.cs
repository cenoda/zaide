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
/// Phase 9 M1 DI integration tests. Verifies that the production composition
/// root resolves <see cref="CommandPaletteViewModel"/>, which registers
/// <c>palette.open</c> in the shared <see cref="ICommandRegistry"/> singleton
/// before keybinding materialization occurs.
/// </summary>
public sealed class Phase9M1DiIntegrationTests
{
    static Phase9M1DiIntegrationTests()
    {
        // ReactiveUI must be initialized before resolving ViewModels that use
        // WhenAnyValue/RaiseAndSetIfChanged in their constructors.
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    /// <summary>
    /// Builds the production container, substituting only the Avalonia scheduler
    /// with a test-safe one so resolution does not require a running UI host.
    /// Mirrors the pattern from <see cref="Phase83M3DependencyInjectionTests"/>.
    /// </summary>
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        Program.ConfigureServices(services);
        services.AddSingleton<IScheduler>(_ => CurrentThreadScheduler.Instance);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void CommandPaletteViewModel_ResolvesFromProductionContainer()
    {
        using var provider = BuildProvider();

        var paletteVm = provider.GetRequiredService<CommandPaletteViewModel>();

        Assert.NotNull(paletteVm);
    }

    [Fact]
    public void PaletteOpen_IsRegisteredInRegistry_AfterPaletteVmResolution()
    {
        using var provider = BuildProvider();

        // Phase 9 M1: resolve the palette VM first (as production startup does).
        provider.GetRequiredService<CommandPaletteViewModel>();

        // Now the singleton ICommandRegistry must contain palette.open.
        var registry = provider.GetRequiredService<ICommandRegistry>();
        var descriptor = registry.GetById("palette.open");

        Assert.NotNull(descriptor);
        Assert.Equal("palette.open", descriptor!.Id);
        Assert.Equal("Open Command Palette", descriptor.DisplayName);
        Assert.Equal("Palette", descriptor.Category);
        Assert.Contains("Ctrl+Shift+P", descriptor.DefaultGestures);
        Assert.True(descriptor.Command.CanExecute(null));
    }

    [Fact]
    public void PaletteOpen_SingletonRegistry_SharedAcrossResolutions()
    {
        using var provider = BuildProvider();

        // Resolve the palette VM, which registers palette.open in the singleton.
        provider.GetRequiredService<CommandPaletteViewModel>();

        // Subsequent resolutions see the same registry with palette.open.
        var registry1 = provider.GetRequiredService<ICommandRegistry>();
        var registry2 = provider.GetRequiredService<ICommandRegistry>();

        Assert.Same(registry1, registry2);
        Assert.Same(
            registry1.GetById("palette.open"),
            registry2.GetById("palette.open"));
    }
}
