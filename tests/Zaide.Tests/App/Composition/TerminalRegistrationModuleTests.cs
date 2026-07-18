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
using Zaide.Features.Terminal.Contracts;
using Zaide.Features.Terminal.Infrastructure;
using Zaide.Features.Terminal.Presentation;

namespace Zaide.Tests.App.Composition;

/// <summary>
/// Refactor 6.3 M6e: proves Terminal DI membership moved into
/// <see cref="TerminalServiceCollectionExtensions.AddZaideTerminal"/> without
/// changing service types, lifetimes, mappings, or total registration
/// membership.
/// </summary>
public sealed class TerminalRegistrationModuleTests
{
    private static readonly string[] TerminalServiceTypeNames =
    {
        typeof(ITerminalServiceFactory).FullName!,
        typeof(ITerminalHost).FullName!,
    };

    private static readonly string[] M6jPlusDirectMarkers =
    {
        "AddSingleton<ILanguageSessionService, LanguageSessionService>()",
        "AddSingleton<IDebugSessionService, DebugSessionService>()",
    };

    static TerminalRegistrationModuleTests()
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
    public void AddZaideTerminal_RegistersExactlyTwoPlannedServices()
    {
        var services = new ServiceCollection();
        var returned = services.AddZaideTerminal();

        Assert.Same(services, returned);
        Assert.Equal(2, services.Count);
        Assert.All(services, d => Assert.Equal(ServiceLifetime.Singleton, d.Lifetime));

        var serviceTypes = services
            .Select(d => d.ServiceType.FullName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        var expected = TerminalServiceTypeNames
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected, serviceTypes);

        Assert.Contains(
            services,
            d => d.ServiceType == typeof(ITerminalServiceFactory)
                && d.ImplementationType == typeof(LinuxTerminalServiceFactory));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(ITerminalHost)
                && d.ImplementationType == typeof(TerminalHost));
    }

    [Fact]
    public void ProgramConfigureServices_ResolvesTerminalServicesAsSingletons()
    {
        using var provider = BuildProductionProvider();

        var factory1 = provider.GetRequiredService<ITerminalServiceFactory>();
        var factory2 = provider.GetRequiredService<ITerminalServiceFactory>();
        Assert.Same(factory1, factory2);
        Assert.IsType<LinuxTerminalServiceFactory>(factory1);

        var host1 = provider.GetRequiredService<ITerminalHost>();
        var host2 = provider.GetRequiredService<ITerminalHost>();
        Assert.Same(host1, host2);
        Assert.IsType<TerminalHost>(host1);
    }

    [Fact]
    public void ProgramSource_CallsAddZaideTerminalOnce_AndDoesNotDeclareTerminalRegistrations()
    {
        var programSource = ReadRepoFile("src/App/Composition/Program.cs");

        Assert.Single(Regex.Matches(programSource, @"AddZaideAppCore\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideSettings\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideWorkspace\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideEditor\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideTerminal\s*\(\s*\)"));

        var appCoreIndex = programSource.IndexOf("AddZaideAppCore()", StringComparison.Ordinal);
        var settingsIndex = programSource.IndexOf("AddZaideSettings()", StringComparison.Ordinal);
        var workspaceIndex = programSource.IndexOf("AddZaideWorkspace()", StringComparison.Ordinal);
        var editorIndex = programSource.IndexOf("AddZaideEditor()", StringComparison.Ordinal);
        var terminalIndex = programSource.IndexOf("AddZaideTerminal()", StringComparison.Ordinal);
        Assert.True(appCoreIndex >= 0);
        Assert.True(settingsIndex > appCoreIndex);
        Assert.True(workspaceIndex > settingsIndex);
        Assert.True(editorIndex > workspaceIndex);
        Assert.True(terminalIndex > editorIndex);

        Assert.DoesNotContain(
            "AddSingleton<ITerminalServiceFactory, LinuxTerminalServiceFactory>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<ITerminalHost, TerminalHost>()",
            programSource);

        // AddLogging remains in Program (not an M6e registration).
        Assert.Contains("AddLogging(", programSource);
    }

    [Fact]
    public void TerminalModuleSource_ContainsExactlyTheTwoPlannedRegistrations()
    {
        var moduleSource = ReadRepoFile(
            "src/App/Composition/Registration/TerminalServiceCollectionExtensions.cs");

        Assert.Contains(
            "internal static class TerminalServiceCollectionExtensions",
            moduleSource);
        Assert.Contains("internal static IServiceCollection AddZaideTerminal", moduleSource);

        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<ITerminalServiceFactory,\s*LinuxTerminalServiceFactory>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<ITerminalHost,\s*TerminalHost>\(\)"));

        Assert.Equal(2, Regex.Matches(moduleSource, @"AddSingleton<").Count);
    }

    [Fact]
    public void ProgramSource_StillDeclaresM6jPlusRegistrationsDirectly()
    {
        var programSource = ReadRepoFile("src/App/Composition/Program.cs");

        foreach (var marker in M6jPlusDirectMarkers)
        {
            Assert.Contains(marker, programSource);
        }

        // M6f Agents, M6g Townhall, M6h SourceControl, and M6i ProjectSystem modules are present; M6j–M6k do not exist yet.
        Assert.Single(Regex.Matches(programSource, @"AddZaideAgents\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideTownhall\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideSourceControl\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideProjectSystem\s*\(\s*\)"));
        Assert.DoesNotContain("AddZaideLanguage", programSource);
        Assert.DoesNotContain("AddZaideDebugging", programSource);
    }
}
