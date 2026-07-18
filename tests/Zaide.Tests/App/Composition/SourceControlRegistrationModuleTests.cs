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
using Zaide.Features.SourceControl.Application;
using Zaide.Features.SourceControl.Contracts;
using Zaide.Features.SourceControl.Infrastructure;
using Zaide.Features.SourceControl.Presentation;

namespace Zaide.Tests.App.Composition;

/// <summary>
/// Refactor 6.3 M6h: proves SourceControl DI membership moved into
/// <see cref="SourceControlServiceCollectionExtensions.AddZaideSourceControl"/> without
/// changing service types, lifetimes, mappings, or total registration membership.
/// </summary>
public sealed class SourceControlRegistrationModuleTests
{
    private static readonly string[] SourceControlServiceTypeNames =
    {
        typeof(SourceControlViewModel).FullName!,
        typeof(IGitRepositoryService).FullName!,
        typeof(ISourceControlSnapshotOrchestrator).FullName!,
        typeof(IFileDiffService).FullName!,
        typeof(ISourceControlDiffTabService).FullName!,
        typeof(IGitMutationService).FullName!,
    };


    static SourceControlRegistrationModuleTests()
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
    public void AddZaideSourceControl_RegistersExactlySixPlannedServices()
    {
        var services = new ServiceCollection();
        var returned = services.AddZaideSourceControl();

        Assert.Same(services, returned);
        Assert.Equal(6, services.Count);
        Assert.All(services, d => Assert.Equal(ServiceLifetime.Singleton, d.Lifetime));

        var serviceTypes = services
            .Select(d => d.ServiceType.FullName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        var expected = SourceControlServiceTypeNames
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected, serviceTypes);

        // SourceControlViewModel remains self-registered.
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(SourceControlViewModel)
                && d.ImplementationType == typeof(SourceControlViewModel));

        // Five interface-to-implementation mappings remain unchanged.
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IGitRepositoryService)
                && d.ImplementationType == typeof(GitRepositoryService));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(ISourceControlSnapshotOrchestrator)
                && d.ImplementationType == typeof(SourceControlSnapshotOrchestrator));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IFileDiffService)
                && d.ImplementationType == typeof(FileDiffService));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(ISourceControlDiffTabService)
                && d.ImplementationType == typeof(SourceControlDiffTabService));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IGitMutationService)
                && d.ImplementationType == typeof(GitMutationService));
    }

    [Fact]
    public void ProgramConfigureServices_ResolvesSourceControlServicesAsSingletons()
    {
        using var provider = BuildProductionProvider();

        var viewModel1 = provider.GetRequiredService<SourceControlViewModel>();
        var viewModel2 = provider.GetRequiredService<SourceControlViewModel>();
        Assert.Same(viewModel1, viewModel2);

        var gitRepo1 = provider.GetRequiredService<IGitRepositoryService>();
        var gitRepo2 = provider.GetRequiredService<IGitRepositoryService>();
        Assert.Same(gitRepo1, gitRepo2);
        Assert.IsType<GitRepositoryService>(gitRepo1);

        var orchestrator1 = provider.GetRequiredService<ISourceControlSnapshotOrchestrator>();
        var orchestrator2 = provider.GetRequiredService<ISourceControlSnapshotOrchestrator>();
        Assert.Same(orchestrator1, orchestrator2);
        Assert.IsType<SourceControlSnapshotOrchestrator>(orchestrator1);

        var fileDiff1 = provider.GetRequiredService<IFileDiffService>();
        var fileDiff2 = provider.GetRequiredService<IFileDiffService>();
        Assert.Same(fileDiff1, fileDiff2);
        Assert.IsType<FileDiffService>(fileDiff1);

        var diffTab1 = provider.GetRequiredService<ISourceControlDiffTabService>();
        var diffTab2 = provider.GetRequiredService<ISourceControlDiffTabService>();
        Assert.Same(diffTab1, diffTab2);
        Assert.IsType<SourceControlDiffTabService>(diffTab1);

        var mutation1 = provider.GetRequiredService<IGitMutationService>();
        var mutation2 = provider.GetRequiredService<IGitMutationService>();
        Assert.Same(mutation1, mutation2);
        Assert.IsType<GitMutationService>(mutation1);
    }

    [Fact]
    public void ProgramSource_CallsAddZaideSourceControlOnce_AndDoesNotDeclareSourceControlRegistrations()
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

        var appCoreIndex = programSource.IndexOf("AddZaideAppCore()", StringComparison.Ordinal);
        var settingsIndex = programSource.IndexOf("AddZaideSettings()", StringComparison.Ordinal);
        var workspaceIndex = programSource.IndexOf("AddZaideWorkspace()", StringComparison.Ordinal);
        var editorIndex = programSource.IndexOf("AddZaideEditor()", StringComparison.Ordinal);
        var terminalIndex = programSource.IndexOf("AddZaideTerminal()", StringComparison.Ordinal);
        var agentsIndex = programSource.IndexOf("AddZaideAgents()", StringComparison.Ordinal);
        var townhallIndex = programSource.IndexOf("AddZaideTownhall()", StringComparison.Ordinal);
        var sourceControlIndex = programSource.IndexOf("AddZaideSourceControl()", StringComparison.Ordinal);
        Assert.True(appCoreIndex >= 0);
        Assert.True(settingsIndex > appCoreIndex);
        Assert.True(workspaceIndex > settingsIndex);
        Assert.True(editorIndex > workspaceIndex);
        Assert.True(terminalIndex > editorIndex);
        Assert.True(agentsIndex > terminalIndex);
        Assert.True(townhallIndex > agentsIndex);
        Assert.True(sourceControlIndex > townhallIndex);

        Assert.DoesNotContain("AddSingleton<SourceControlViewModel>()", programSource);
        Assert.DoesNotContain(
            "AddSingleton<IGitRepositoryService, GitRepositoryService>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<ISourceControlSnapshotOrchestrator, SourceControlSnapshotOrchestrator>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<IFileDiffService, FileDiffService>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<ISourceControlDiffTabService, SourceControlDiffTabService>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<IGitMutationService, GitMutationService>()",
            programSource);

        // AddLogging remains in Program (not an M6h registration).
        Assert.Contains("AddLogging(", programSource);
    }

    [Fact]
    public void SourceControlModuleSource_ContainsExactlyTheSixPlannedRegistrations()
    {
        var moduleSource = ReadRepoFile(
            "src/App/Composition/Registration/SourceControlServiceCollectionExtensions.cs");

        Assert.Contains(
            "internal static class SourceControlServiceCollectionExtensions",
            moduleSource);
        Assert.Contains(
            "internal static IServiceCollection AddZaideSourceControl",
            moduleSource);

        Assert.Single(Regex.Matches(moduleSource, @"AddSingleton<SourceControlViewModel>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IGitRepositoryService,\s*GitRepositoryService>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<ISourceControlSnapshotOrchestrator,\s*SourceControlSnapshotOrchestrator>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IFileDiffService,\s*FileDiffService>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<ISourceControlDiffTabService,\s*SourceControlDiffTabService>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IGitMutationService,\s*GitMutationService>\(\)"));

        Assert.Equal(6, Regex.Matches(moduleSource, @"AddSingleton<").Count);
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
