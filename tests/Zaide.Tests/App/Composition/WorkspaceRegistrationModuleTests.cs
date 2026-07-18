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
using Zaide.Features.Workspace.Contracts;
using Zaide.Features.Workspace.Infrastructure;
using Zaide.Features.Workspace.Presentation;

namespace Zaide.Tests.App.Composition;

/// <summary>
/// Refactor 6.3 M6c: proves Workspace DI membership moved into
/// <see cref="WorkspaceServiceCollectionExtensions.AddZaideWorkspace"/> without
/// changing service types, lifetimes, or total registration membership.
/// </summary>
public sealed class WorkspaceRegistrationModuleTests
{
    private static readonly string[] WorkspaceServiceTypeNames =
    {
        typeof(IFileTreeService).FullName!,
        typeof(FileTreeViewModel).FullName!,
    };

    private static readonly string[] M6ePlusDirectMarkers =
    {
        "AddSingleton<ITerminalServiceFactory, LinuxTerminalServiceFactory>()",
        "AddSingleton<ITerminalHost, TerminalHost>()",
        "AddSingleton<IAgentPanelHost, AgentPanelHost>()",
        "AddSingleton<IAgentExecutionService, AgentExecutionService>()",
        "AddSingleton<TownhallState>()",
        "AddSingleton<SourceControlViewModel>()",
        "AddSingleton<IProjectContextService, ProjectContextService>()",
        "AddSingleton<ILanguageSessionService, LanguageSessionService>()",
        "AddSingleton<IDebugSessionService, DebugSessionService>()",
    };

    static WorkspaceRegistrationModuleTests()
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
    public void AddZaideWorkspace_RegistersExactlyTwoPlannedServices()
    {
        var services = new ServiceCollection();
        services.AddZaideWorkspace();

        Assert.Equal(2, services.Count);
        Assert.All(services, d => Assert.Equal(ServiceLifetime.Singleton, d.Lifetime));

        var serviceTypes = services
            .Select(d => d.ServiceType.FullName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        var expected = WorkspaceServiceTypeNames
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected, serviceTypes);

        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IFileTreeService)
                && d.ImplementationType == typeof(FileTreeService));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(FileTreeViewModel)
                && d.ImplementationType == typeof(FileTreeViewModel));
    }

    [Fact]
    public void ProgramConfigureServices_ResolvesWorkspaceServicesAsSingletons()
    {
        using var provider = BuildProductionProvider();

        var fileTree1 = provider.GetRequiredService<IFileTreeService>();
        var fileTree2 = provider.GetRequiredService<IFileTreeService>();
        Assert.Same(fileTree1, fileTree2);
        Assert.IsType<FileTreeService>(fileTree1);

        var viewModel1 = provider.GetRequiredService<FileTreeViewModel>();
        var viewModel2 = provider.GetRequiredService<FileTreeViewModel>();
        Assert.Same(viewModel1, viewModel2);
    }

    [Fact]
    public void ProgramSource_CallsAddZaideWorkspaceOnce_AndDoesNotDeclareWorkspaceRegistrations()
    {
        var programSource = ReadRepoFile("src/App/Composition/Program.cs");

        Assert.Single(Regex.Matches(programSource, @"AddZaideAppCore\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideSettings\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideWorkspace\s*\(\s*\)"));

        var appCoreIndex = programSource.IndexOf("AddZaideAppCore()", StringComparison.Ordinal);
        var settingsIndex = programSource.IndexOf("AddZaideSettings()", StringComparison.Ordinal);
        var workspaceIndex = programSource.IndexOf("AddZaideWorkspace()", StringComparison.Ordinal);
        Assert.True(appCoreIndex >= 0);
        Assert.True(settingsIndex > appCoreIndex);
        Assert.True(workspaceIndex > settingsIndex);

        Assert.DoesNotContain(
            "AddSingleton<IFileTreeService, FileTreeService>()",
            programSource);
        Assert.DoesNotContain("AddSingleton<FileTreeViewModel>()", programSource);

        // AddLogging remains in Program (not an M6c registration).
        Assert.Contains("AddLogging(", programSource);
    }

    [Fact]
    public void WorkspaceModuleSource_ContainsExactlyTheTwoPlannedRegistrations()
    {
        var moduleSource = ReadRepoFile(
            "src/App/Composition/Registration/WorkspaceServiceCollectionExtensions.cs");

        Assert.Contains(
            "internal static class WorkspaceServiceCollectionExtensions",
            moduleSource);
        Assert.Contains("internal static IServiceCollection AddZaideWorkspace", moduleSource);

        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IFileTreeService,\s*FileTreeService>\(\)"));
        Assert.Single(Regex.Matches(moduleSource, @"AddSingleton<FileTreeViewModel>\(\)"));

        Assert.Equal(2, Regex.Matches(moduleSource, @"AddSingleton<").Count);

        // Domain Workspace remains owned by AppCore (M6a), not this module.
        Assert.DoesNotContain("AddSingleton<Workspace>()", moduleSource);
    }

    [Fact]
    public void ProgramSource_StillDeclaresM6ePlusRegistrationsDirectly()
    {
        var programSource = ReadRepoFile("src/App/Composition/Program.cs");

        foreach (var marker in M6ePlusDirectMarkers)
        {
            Assert.Contains(marker, programSource);
        }

        // M6d Editor module is present; M6e–M6k modules do not exist yet.
        Assert.Single(Regex.Matches(programSource, @"AddZaideEditor\s*\(\s*\)"));
        Assert.DoesNotContain("AddZaideTerminal", programSource);
        Assert.DoesNotContain("AddZaideAgents", programSource);
        Assert.DoesNotContain("AddZaideTownhall", programSource);
        Assert.DoesNotContain("AddZaideSourceControl", programSource);
        Assert.DoesNotContain("AddZaideProjectSystem", programSource);
        Assert.DoesNotContain("AddZaideLanguage", programSource);
        Assert.DoesNotContain("AddZaideDebugging", programSource);
    }
}
