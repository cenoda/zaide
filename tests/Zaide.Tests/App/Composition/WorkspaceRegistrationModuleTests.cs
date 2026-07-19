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
        Assert.Single(Regex.Matches(programSource, @"AddZaideConversations\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideSettings\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideWorkspace\s*\(\s*\)"));

        var appCoreIndex = programSource.IndexOf("AddZaideAppCore()", StringComparison.Ordinal);
        var conversationsIndex = programSource.IndexOf("AddZaideConversations()", StringComparison.Ordinal);
        var settingsIndex = programSource.IndexOf("AddZaideSettings()", StringComparison.Ordinal);
        var workspaceIndex = programSource.IndexOf("AddZaideWorkspace()", StringComparison.Ordinal);
        Assert.True(appCoreIndex >= 0);
        Assert.True(conversationsIndex > appCoreIndex);
        Assert.True(settingsIndex > conversationsIndex);
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
    public void ProgramSource_CallsAllTwelveModules_AndHasNoDirectProductionAddSingleton()
    {
        var programSource = ReadRepoFile("src/App/Composition/Program.cs");

        Assert.Single(Regex.Matches(programSource, @"AddZaideAppCore\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideConversations\s*\(\s*\)"));
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
        var conversationsIndex = programSource.IndexOf("AddZaideConversations()", StringComparison.Ordinal);
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
        Assert.True(conversationsIndex > appCoreIndex);
        Assert.True(settingsIndex > conversationsIndex);
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
