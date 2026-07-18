using System;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.Builder;
using Xunit;
using Zaide;
using Zaide.App.Composition;
using Zaide.App.Composition.Registration;
using Zaide.App.Shell;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Tests.App.Composition;

/// <summary>
/// Refactor 6.3 M6a: proves AppCore DI membership moved into
/// <see cref="AppCoreServiceCollectionExtensions.AddZaideAppCore"/> without
/// changing service types, lifetimes, or total registration membership.
/// </summary>
public sealed class AppCoreRegistrationModuleTests
{
    private static readonly string[] AppCoreServiceTypeNames =
    {
        typeof(Workspace).FullName!,
        typeof(ICommandRegistry).FullName!,
        typeof(StatusBarViewModel).FullName!,
        typeof(IScheduler).FullName!,
        typeof(MainWindowViewModel).FullName!,
        typeof(CommandPaletteViewModel).FullName!,
    };

    private static readonly string[] M6kPlusDirectMarkers =
    {
        "AddSingleton<IDebugSessionService, DebugSessionService>()",
    };

    static AppCoreRegistrationModuleTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private static ServiceProvider BuildProductionProvider()
    {
        var services = new ServiceCollection();
        Program.ConfigureServices(services);
        services.AddSingleton<IScheduler>(_ => CurrentThreadScheduler.Instance);
        return services.BuildServiceProvider();
    }

    private static string ReadRepoFile(string relativePath)
    {
        var root = FindRepositoryRoot();
        return File.ReadAllText(
            Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Zaide.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root (Zaide.slnx).");
    }

    [Fact]
    public void AddZaideAppCore_RegistersExactlySixPlannedServices()
    {
        var services = new ServiceCollection();
        services.AddZaideAppCore();

        Assert.Equal(6, services.Count);
        Assert.All(services, d => Assert.Equal(ServiceLifetime.Singleton, d.Lifetime));

        var serviceTypes = services
            .Select(d => d.ServiceType.FullName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        var expected = AppCoreServiceTypeNames
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected, serviceTypes);

        Assert.Contains(
            services,
            d => d.ServiceType == typeof(ICommandRegistry)
                && d.ImplementationType == typeof(CommandRegistry));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IScheduler)
                && d.ImplementationFactory is not null);
    }

    [Fact]
    public void ProgramConfigureServices_ResolvesAppCoreServicesAsSingletons()
    {
        using var provider = BuildProductionProvider();

        var workspace1 = provider.GetRequiredService<Workspace>();
        var workspace2 = provider.GetRequiredService<Workspace>();
        Assert.Same(workspace1, workspace2);

        var registry1 = provider.GetRequiredService<ICommandRegistry>();
        var registry2 = provider.GetRequiredService<ICommandRegistry>();
        Assert.Same(registry1, registry2);
        Assert.IsType<CommandRegistry>(registry1);

        var status1 = provider.GetRequiredService<StatusBarViewModel>();
        var status2 = provider.GetRequiredService<StatusBarViewModel>();
        Assert.Same(status1, status2);

        var scheduler1 = provider.GetRequiredService<IScheduler>();
        var scheduler2 = provider.GetRequiredService<IScheduler>();
        Assert.Same(scheduler1, scheduler2);

        var main1 = provider.GetRequiredService<MainWindowViewModel>();
        var main2 = provider.GetRequiredService<MainWindowViewModel>();
        Assert.Same(main1, main2);

        var palette1 = provider.GetRequiredService<CommandPaletteViewModel>();
        var palette2 = provider.GetRequiredService<CommandPaletteViewModel>();
        Assert.Same(palette1, palette2);
    }

    [Fact]
    public void ProgramSource_CallsAddZaideAppCoreOnce_AndDoesNotDeclareAppCoreRegistrations()
    {
        var programSource = ReadRepoFile("src/App/Composition/Program.cs");

        Assert.Single(Regex.Matches(programSource, @"AddZaideAppCore\s*\(\s*\)"));

        Assert.DoesNotContain("AddSingleton<Workspace>()", programSource);
        Assert.DoesNotContain(
            "AddSingleton<ICommandRegistry, CommandRegistry>()",
            programSource);
        Assert.DoesNotContain("AddSingleton<StatusBarViewModel>()", programSource);
        Assert.DoesNotContain("AddSingleton<IScheduler>", programSource);
        Assert.DoesNotContain("AddSingleton<MainWindowViewModel>()", programSource);
        Assert.DoesNotContain("AddSingleton<CommandPaletteViewModel>()", programSource);

        // AddLogging remains in Program (not an M6a registration).
        Assert.Contains("AddLogging(", programSource);
    }

    [Fact]
    public void AppCoreModuleSource_ContainsExactlyTheSixPlannedRegistrations()
    {
        var moduleSource = ReadRepoFile(
            "src/App/Composition/Registration/AppCoreServiceCollectionExtensions.cs");

        Assert.Contains("internal static class AppCoreServiceCollectionExtensions", moduleSource);
        Assert.Contains("internal static IServiceCollection AddZaideAppCore", moduleSource);

        Assert.Single(Regex.Matches(moduleSource, @"AddSingleton<Workspace>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<ICommandRegistry,\s*CommandRegistry>\(\)"));
        Assert.Single(Regex.Matches(moduleSource, @"AddSingleton<StatusBarViewModel>\(\)"));
        Assert.Single(Regex.Matches(moduleSource, @"AddSingleton<IScheduler>"));
        Assert.Single(Regex.Matches(moduleSource, @"AddSingleton<MainWindowViewModel>\(\)"));
        Assert.Single(Regex.Matches(moduleSource, @"AddSingleton<CommandPaletteViewModel>\(\)"));

        Assert.Equal(6, Regex.Matches(moduleSource, @"AddSingleton<").Count);
        Assert.Contains("AvaloniaScheduler.Instance", moduleSource);
    }

    [Fact]
    public void ProgramSource_StillDeclaresM6kPlusRegistrationsDirectly()
    {
        var programSource = ReadRepoFile("src/App/Composition/Program.cs");

        foreach (var marker in M6kPlusDirectMarkers)
        {
            Assert.Contains(marker, programSource);
        }

        // M6b–M6j modules are present; M6k does not exist yet.
        Assert.Single(Regex.Matches(programSource, @"AddZaideSettings\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideWorkspace\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideEditor\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideTerminal\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideAgents\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideTownhall\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideSourceControl\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideProjectSystem\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideLanguage\s*\(\s*\)"));
        Assert.DoesNotContain("AddZaideDebugging", programSource);
    }
}
