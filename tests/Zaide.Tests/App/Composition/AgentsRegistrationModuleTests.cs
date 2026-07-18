using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive.Concurrency;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.Builder;
using Xunit;
using Zaide;
using Zaide.App.Composition;
using Zaide.App.Composition.Registration;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Agents.Presentation;

namespace Zaide.Tests.App.Composition;

/// <summary>
/// Refactor 6.3 M6f: proves Agents DI membership moved into
/// <see cref="AgentsServiceCollectionExtensions.AddZaideAgents"/> without
/// changing service types, lifetimes, mappings, factory behavior, or total
/// registration membership. Registration performs no external network activity.
/// </summary>
public sealed class AgentsRegistrationModuleTests
{
    private static readonly string[] AgentsServiceTypeNames =
    {
        typeof(IAgentPanelHost).FullName!,
        typeof(IAgentExecutionService).FullName!,
        typeof(IAgentExecutionCoordinator).FullName!,
        typeof(MentionParser).FullName!,
        typeof(IAgentRouter).FullName!,
        typeof(HttpClient).FullName!,
    };


    static AgentsRegistrationModuleTests()
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
    public void AddZaideAgents_RegistersExactlySixPlannedServices()
    {
        var services = new ServiceCollection();
        var returned = services.AddZaideAgents();

        Assert.Same(services, returned);
        Assert.Equal(6, services.Count);
        Assert.All(services, d => Assert.Equal(ServiceLifetime.Singleton, d.Lifetime));

        var serviceTypes = services
            .Select(d => d.ServiceType.FullName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        var expected = AgentsServiceTypeNames
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected, serviceTypes);

        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IAgentPanelHost)
                && d.ImplementationType == typeof(AgentPanelHost));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IAgentExecutionService)
                && d.ImplementationType == typeof(AgentExecutionService));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IAgentExecutionCoordinator)
                && d.ImplementationType == typeof(AgentExecutionCoordinator));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(MentionParser)
                && d.ImplementationType == typeof(MentionParser));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IAgentRouter)
                && d.ImplementationType == typeof(AgentRouter));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(HttpClient)
                && d.ImplementationFactory is not null
                && d.ImplementationType is null);
    }

    [Fact]
    public void ProgramConfigureServices_ResolvesAgentsServicesAsSingletons()
    {
        using var provider = BuildProductionProvider();

        var panelHost1 = provider.GetRequiredService<IAgentPanelHost>();
        var panelHost2 = provider.GetRequiredService<IAgentPanelHost>();
        Assert.Same(panelHost1, panelHost2);
        Assert.IsType<AgentPanelHost>(panelHost1);

        var executionService1 = provider.GetRequiredService<IAgentExecutionService>();
        var executionService2 = provider.GetRequiredService<IAgentExecutionService>();
        Assert.Same(executionService1, executionService2);
        Assert.IsType<AgentExecutionService>(executionService1);

        var coordinator1 = provider.GetRequiredService<IAgentExecutionCoordinator>();
        var coordinator2 = provider.GetRequiredService<IAgentExecutionCoordinator>();
        Assert.Same(coordinator1, coordinator2);
        Assert.IsType<AgentExecutionCoordinator>(coordinator1);

        var parser1 = provider.GetRequiredService<MentionParser>();
        var parser2 = provider.GetRequiredService<MentionParser>();
        Assert.Same(parser1, parser2);

        var router1 = provider.GetRequiredService<IAgentRouter>();
        var router2 = provider.GetRequiredService<IAgentRouter>();
        Assert.Same(router1, router2);
        Assert.IsType<AgentRouter>(router1);

        // Resolving HttpClient constructs a client only; no network request is issued.
        var httpClient1 = provider.GetRequiredService<HttpClient>();
        var httpClient2 = provider.GetRequiredService<HttpClient>();
        Assert.Same(httpClient1, httpClient2);
        Assert.Equal(TimeSpan.FromSeconds(120), httpClient1.Timeout);
    }

    [Fact]
    public void ProgramSource_CallsAddZaideAgentsOnce_AndDoesNotDeclareAgentsRegistrations()
    {
        var programSource = ReadRepoFile("src/App/Composition/Program.cs");

        Assert.Single(Regex.Matches(programSource, @"AddZaideAppCore\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideSettings\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideWorkspace\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideEditor\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideTerminal\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideAgents\s*\(\s*\)"));

        var appCoreIndex = programSource.IndexOf("AddZaideAppCore()", StringComparison.Ordinal);
        var settingsIndex = programSource.IndexOf("AddZaideSettings()", StringComparison.Ordinal);
        var workspaceIndex = programSource.IndexOf("AddZaideWorkspace()", StringComparison.Ordinal);
        var editorIndex = programSource.IndexOf("AddZaideEditor()", StringComparison.Ordinal);
        var terminalIndex = programSource.IndexOf("AddZaideTerminal()", StringComparison.Ordinal);
        var agentsIndex = programSource.IndexOf("AddZaideAgents()", StringComparison.Ordinal);
        Assert.True(appCoreIndex >= 0);
        Assert.True(settingsIndex > appCoreIndex);
        Assert.True(workspaceIndex > settingsIndex);
        Assert.True(editorIndex > workspaceIndex);
        Assert.True(terminalIndex > editorIndex);
        Assert.True(agentsIndex > terminalIndex);

        Assert.DoesNotContain(
            "AddSingleton<IAgentPanelHost, AgentPanelHost>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<IAgentExecutionService, AgentExecutionService>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<IAgentExecutionCoordinator, AgentExecutionCoordinator>()",
            programSource);
        Assert.DoesNotContain("AddSingleton<MentionParser>()", programSource);
        Assert.DoesNotContain(
            "AddSingleton<IAgentRouter, AgentRouter>()",
            programSource);
        Assert.DoesNotContain("new HttpClient()", programSource);
        Assert.DoesNotContain("TimeSpan.FromSeconds(120)", programSource);

        // AddLogging remains in Program (not an M6f registration).
        Assert.Contains("AddLogging(", programSource);
    }

    [Fact]
    public void AgentsModuleSource_ContainsExactlyTheSixPlannedRegistrations()
    {
        var moduleSource = ReadRepoFile(
            "src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs");

        Assert.Contains(
            "internal static class AgentsServiceCollectionExtensions",
            moduleSource);
        Assert.Contains("internal static IServiceCollection AddZaideAgents", moduleSource);

        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IAgentPanelHost,\s*AgentPanelHost>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IAgentExecutionService,\s*AgentExecutionService>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IAgentExecutionCoordinator,\s*AgentExecutionCoordinator>\(\)"));
        Assert.Single(Regex.Matches(moduleSource, @"AddSingleton<MentionParser>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IAgentRouter,\s*AgentRouter>\(\)"));
        Assert.Single(Regex.Matches(moduleSource, @"new HttpClient\(\)"));
        Assert.Contains("TimeSpan.FromSeconds(120)", moduleSource);

        Assert.Equal(6, Regex.Matches(moduleSource, @"AddSingleton").Count);
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

        // M7: CompositionRoot store assigned in Program; no fictitious registration module.
        Assert.Contains("CompositionRoot.Services = sp!", programSource);
        Assert.DoesNotContain("App.Services", programSource);
        Assert.DoesNotContain("AddZaideCompositionRoot", programSource);
    }
}
