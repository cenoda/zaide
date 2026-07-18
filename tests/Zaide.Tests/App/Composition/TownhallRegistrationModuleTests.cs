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
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;

namespace Zaide.Tests.App.Composition;

/// <summary>
/// Refactor 6.3 M6g: proves Townhall DI membership moved into
/// <see cref="TownhallServiceCollectionExtensions.AddZaideTownhall"/> without
/// changing service types, lifetimes, mappings, or total registration membership.
/// </summary>
public sealed class TownhallRegistrationModuleTests
{
    private static readonly string[] TownhallServiceTypeNames =
    {
        typeof(TownhallState).FullName!,
        typeof(TownhallViewModel).FullName!,
    };


    static TownhallRegistrationModuleTests()
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
    public void AddZaideTownhall_RegistersExactlyTwoPlannedServices()
    {
        var services = new ServiceCollection();
        var returned = services.AddZaideTownhall();

        Assert.Same(services, returned);
        Assert.Equal(2, services.Count);
        Assert.All(services, d => Assert.Equal(ServiceLifetime.Singleton, d.Lifetime));

        var serviceTypes = services
            .Select(d => d.ServiceType.FullName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        var expected = TownhallServiceTypeNames
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected, serviceTypes);

        // Both remain self-registrations (ServiceType == ImplementationType).
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(TownhallState)
                && d.ImplementationType == typeof(TownhallState));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(TownhallViewModel)
                && d.ImplementationType == typeof(TownhallViewModel));
    }

    [Fact]
    public void ProgramConfigureServices_ResolvesTownhallServicesAsSingletons()
    {
        using var provider = BuildProductionProvider();

        var state1 = provider.GetRequiredService<TownhallState>();
        var state2 = provider.GetRequiredService<TownhallState>();
        Assert.Same(state1, state2);

        var viewModel1 = provider.GetRequiredService<TownhallViewModel>();
        var viewModel2 = provider.GetRequiredService<TownhallViewModel>();
        Assert.Same(viewModel1, viewModel2);

        // TownhallViewModel resolves with the registered TownhallState dependency:
        // Channels/Agents are the same collections exposed by the singleton state,
        // and DraftText writes sync through to that shared state instance.
        Assert.Same(state1.Channels, viewModel1.Channels);
        Assert.Same(state1.Agents, viewModel1.Agents);

        var marker = "m6g-townhall-di-singleton-sync";
        viewModel1.DraftText = marker;
        Assert.Equal(marker, state1.DraftText);
    }

    [Fact]
    public void ProgramSource_CallsAddZaideTownhallOnce_AndDoesNotDeclareTownhallRegistrations()
    {
        var programSource = ReadRepoFile("src/App/Composition/Program.cs");

        Assert.Single(Regex.Matches(programSource, @"AddZaideAppCore\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideSettings\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideWorkspace\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideEditor\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideTerminal\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideAgents\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideTownhall\s*\(\s*\)"));

        var appCoreIndex = programSource.IndexOf("AddZaideAppCore()", StringComparison.Ordinal);
        var settingsIndex = programSource.IndexOf("AddZaideSettings()", StringComparison.Ordinal);
        var workspaceIndex = programSource.IndexOf("AddZaideWorkspace()", StringComparison.Ordinal);
        var editorIndex = programSource.IndexOf("AddZaideEditor()", StringComparison.Ordinal);
        var terminalIndex = programSource.IndexOf("AddZaideTerminal()", StringComparison.Ordinal);
        var agentsIndex = programSource.IndexOf("AddZaideAgents()", StringComparison.Ordinal);
        var townhallIndex = programSource.IndexOf("AddZaideTownhall()", StringComparison.Ordinal);
        Assert.True(appCoreIndex >= 0);
        Assert.True(settingsIndex > appCoreIndex);
        Assert.True(workspaceIndex > settingsIndex);
        Assert.True(editorIndex > workspaceIndex);
        Assert.True(terminalIndex > editorIndex);
        Assert.True(agentsIndex > terminalIndex);
        Assert.True(townhallIndex > agentsIndex);

        Assert.DoesNotContain("AddSingleton<TownhallState>()", programSource);
        Assert.DoesNotContain("AddSingleton<TownhallViewModel>()", programSource);

        // AddLogging remains in Program (not an M6g registration).
        Assert.Contains("AddLogging(", programSource);
    }

    [Fact]
    public void TownhallModuleSource_ContainsExactlyTheTwoPlannedRegistrations()
    {
        var moduleSource = ReadRepoFile(
            "src/App/Composition/Registration/TownhallServiceCollectionExtensions.cs");

        Assert.Contains(
            "internal static class TownhallServiceCollectionExtensions",
            moduleSource);
        Assert.Contains("internal static IServiceCollection AddZaideTownhall", moduleSource);

        Assert.Single(Regex.Matches(moduleSource, @"AddSingleton<TownhallState>\(\)"));
        Assert.Single(Regex.Matches(moduleSource, @"AddSingleton<TownhallViewModel>\(\)"));

        Assert.Equal(2, Regex.Matches(moduleSource, @"AddSingleton<").Count);
    }


    [Fact]
    public void ProgramSource_CallsAllElevenModules_AndHasNoDirectProductionAddSingleton()
    {
        var programSource = ReadRepoFile("src/App/Composition/Program.cs");

        Assert.Single(Regex.Matches(programSource, @"AddZaideAppCore\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideSettings\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideWorkspace\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideEditor\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideTerminal\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideAgents\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideTownhall\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideSourceControl\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideProjectSystem\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideLanguage\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideDebugging\s*\(\s*\)"));

        var appCoreIndex = programSource.IndexOf("AddZaideAppCore()", StringComparison.Ordinal);
        var settingsIndex = programSource.IndexOf("AddZaideSettings()", StringComparison.Ordinal);
        var workspaceIndex = programSource.IndexOf("AddZaideWorkspace()", StringComparison.Ordinal);
        var editorIndex = programSource.IndexOf("AddZaideEditor()", StringComparison.Ordinal);
        var terminalIndex = programSource.IndexOf("AddZaideTerminal()", StringComparison.Ordinal);
        var agentsIndex = programSource.IndexOf("AddZaideAgents()", StringComparison.Ordinal);
        var townhallIndex = programSource.IndexOf("AddZaideTownhall()", StringComparison.Ordinal);
        var sourceControlIndex = programSource.IndexOf("AddZaideSourceControl()", StringComparison.Ordinal);
        var projectSystemIndex = programSource.IndexOf("AddZaideProjectSystem()", StringComparison.Ordinal);
        var languageIndex = programSource.IndexOf("AddZaideLanguage()", StringComparison.Ordinal);
        var debuggingIndex = programSource.IndexOf("AddZaideDebugging()", StringComparison.Ordinal);
        Assert.True(appCoreIndex >= 0);
        Assert.True(settingsIndex > appCoreIndex);
        Assert.True(workspaceIndex > settingsIndex);
        Assert.True(editorIndex > workspaceIndex);
        Assert.True(terminalIndex > editorIndex);
        Assert.True(agentsIndex > terminalIndex);
        Assert.True(townhallIndex > agentsIndex);
        Assert.True(sourceControlIndex > townhallIndex);
        Assert.True(projectSystemIndex > sourceControlIndex);
        Assert.True(languageIndex > projectSystemIndex);
        Assert.True(debuggingIndex > languageIndex);

        // M6k moved all Debugging registrations; no direct production AddSingleton remains.
        Assert.DoesNotContain("AddSingleton<", programSource);
        Assert.DoesNotContain("AddSingleton(", programSource);

        // AddLogging remains in Program.
        Assert.Contains("AddLogging(", programSource);

        // No M7 CompositionRoot and no fictitious later registration module.
        Assert.DoesNotContain("CompositionRoot", programSource);
        Assert.DoesNotContain("AddZaideCompositionRoot", programSource);
    }
}
