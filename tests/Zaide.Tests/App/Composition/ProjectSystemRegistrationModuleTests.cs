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
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Infrastructure;
using Zaide.Features.ProjectSystem.Presentation;

namespace Zaide.Tests.App.Composition;

/// <summary>
/// Refactor 6.3 M6i: proves ProjectSystem DI membership moved into
/// <see cref="ProjectSystemServiceCollectionExtensions.AddZaideProjectSystem"/> without
/// changing service types, lifetimes, mappings, or total registration membership.
/// </summary>
public sealed class ProjectSystemRegistrationModuleTests
{
    private static readonly string[] ProjectSystemServiceTypeNames =
    {
        typeof(IProjectFileSystem).FullName!,
        typeof(IProjectDiscovery).FullName!,
        typeof(IProjectContextService).FullName!,
        typeof(IProjectOperationGate).FullName!,
        typeof(IProjectDebugTargetResolver).FullName!,
        typeof(IProjectDebugLaunchService).FullName!,
        typeof(IManagedProcessRunner).FullName!,
        typeof(IProjectWorkflowService).FullName!,
        typeof(IProjectOutputService).FullName!,
        typeof(ProjectWorkflowViewModel).FullName!,
        typeof(IBuildDiagnosticsService).FullName!,
        typeof(ITestResultsService).FullName!,
        typeof(TestResultsViewModel).FullName!,
        typeof(ProblemsViewModel).FullName!,
    };

    private static readonly string[] M6jPlusDirectMarkers =
    {
        "AddSingleton<ILanguageSessionService, LanguageSessionService>()",
        "AddSingleton<IDebugSessionService, DebugSessionService>()",
    };

    static ProjectSystemRegistrationModuleTests()
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
    public void AddZaideProjectSystem_RegistersExactlyFourteenPlannedServices()
    {
        var services = new ServiceCollection();
        var returned = services.AddZaideProjectSystem();

        Assert.Same(services, returned);
        Assert.Equal(14, services.Count);
        Assert.All(services, d => Assert.Equal(ServiceLifetime.Singleton, d.Lifetime));

        var serviceTypes = services
            .Select(d => d.ServiceType.FullName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        var expected = ProjectSystemServiceTypeNames
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected, serviceTypes);

        // Three self-registrations remain.
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(ProjectWorkflowViewModel)
                && d.ImplementationType == typeof(ProjectWorkflowViewModel));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(TestResultsViewModel)
                && d.ImplementationType == typeof(TestResultsViewModel));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(ProblemsViewModel)
                && d.ImplementationType == typeof(ProblemsViewModel));

        // Eleven interface-to-implementation mappings remain unchanged.
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IProjectFileSystem)
                && d.ImplementationType == typeof(FileSystemProjectFileSystem));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IProjectDiscovery)
                && d.ImplementationType == typeof(ProjectDiscovery));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IProjectContextService)
                && d.ImplementationType == typeof(ProjectContextService));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IProjectOperationGate)
                && d.ImplementationType == typeof(ProjectOperationGate));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IProjectDebugTargetResolver)
                && d.ImplementationType == typeof(ProjectDebugTargetResolver));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IProjectDebugLaunchService)
                && d.ImplementationType == typeof(ProjectDebugLaunchService));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IManagedProcessRunner)
                && d.ImplementationType == typeof(ManagedProcessRunner));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IProjectWorkflowService)
                && d.ImplementationType == typeof(ProjectWorkflowService));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IProjectOutputService)
                && d.ImplementationType == typeof(ProjectOutputService));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IBuildDiagnosticsService)
                && d.ImplementationType == typeof(BuildDiagnosticsService));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(ITestResultsService)
                && d.ImplementationType == typeof(TestResultsService));
    }

    [Fact]
    public void ProgramConfigureServices_ResolvesProjectSystemServicesAsSingletons()
    {
        using var provider = BuildProductionProvider();

        var fileSystem1 = provider.GetRequiredService<IProjectFileSystem>();
        var fileSystem2 = provider.GetRequiredService<IProjectFileSystem>();
        Assert.Same(fileSystem1, fileSystem2);
        Assert.IsType<FileSystemProjectFileSystem>(fileSystem1);

        var discovery1 = provider.GetRequiredService<IProjectDiscovery>();
        var discovery2 = provider.GetRequiredService<IProjectDiscovery>();
        Assert.Same(discovery1, discovery2);
        Assert.IsType<ProjectDiscovery>(discovery1);

        var context1 = provider.GetRequiredService<IProjectContextService>();
        var context2 = provider.GetRequiredService<IProjectContextService>();
        Assert.Same(context1, context2);
        Assert.IsType<ProjectContextService>(context1);

        var gate1 = provider.GetRequiredService<IProjectOperationGate>();
        var gate2 = provider.GetRequiredService<IProjectOperationGate>();
        Assert.Same(gate1, gate2);
        Assert.IsType<ProjectOperationGate>(gate1);

        var targetResolver1 = provider.GetRequiredService<IProjectDebugTargetResolver>();
        var targetResolver2 = provider.GetRequiredService<IProjectDebugTargetResolver>();
        Assert.Same(targetResolver1, targetResolver2);
        Assert.IsType<ProjectDebugTargetResolver>(targetResolver1);

        var launch1 = provider.GetRequiredService<IProjectDebugLaunchService>();
        var launch2 = provider.GetRequiredService<IProjectDebugLaunchService>();
        Assert.Same(launch1, launch2);
        Assert.IsType<ProjectDebugLaunchService>(launch1);

        var runner1 = provider.GetRequiredService<IManagedProcessRunner>();
        var runner2 = provider.GetRequiredService<IManagedProcessRunner>();
        Assert.Same(runner1, runner2);
        Assert.IsType<ManagedProcessRunner>(runner1);

        var workflow1 = provider.GetRequiredService<IProjectWorkflowService>();
        var workflow2 = provider.GetRequiredService<IProjectWorkflowService>();
        Assert.Same(workflow1, workflow2);
        Assert.IsType<ProjectWorkflowService>(workflow1);

        var output1 = provider.GetRequiredService<IProjectOutputService>();
        var output2 = provider.GetRequiredService<IProjectOutputService>();
        Assert.Same(output1, output2);
        Assert.IsType<ProjectOutputService>(output1);

        var workflowVm1 = provider.GetRequiredService<ProjectWorkflowViewModel>();
        var workflowVm2 = provider.GetRequiredService<ProjectWorkflowViewModel>();
        Assert.Same(workflowVm1, workflowVm2);

        var buildDiagnostics1 = provider.GetRequiredService<IBuildDiagnosticsService>();
        var buildDiagnostics2 = provider.GetRequiredService<IBuildDiagnosticsService>();
        Assert.Same(buildDiagnostics1, buildDiagnostics2);
        Assert.IsType<BuildDiagnosticsService>(buildDiagnostics1);

        var testResults1 = provider.GetRequiredService<ITestResultsService>();
        var testResults2 = provider.GetRequiredService<ITestResultsService>();
        Assert.Same(testResults1, testResults2);
        Assert.IsType<TestResultsService>(testResults1);

        var testResultsVm1 = provider.GetRequiredService<TestResultsViewModel>();
        var testResultsVm2 = provider.GetRequiredService<TestResultsViewModel>();
        Assert.Same(testResultsVm1, testResultsVm2);

        var problemsVm1 = provider.GetRequiredService<ProblemsViewModel>();
        var problemsVm2 = provider.GetRequiredService<ProblemsViewModel>();
        Assert.Same(problemsVm1, problemsVm2);
    }

    [Fact]
    public void ProgramSource_CallsAddZaideProjectSystemOnce_AndDoesNotDeclareProjectSystemRegistrations()
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

        var appCoreIndex = programSource.IndexOf("AddZaideAppCore()", StringComparison.Ordinal);
        var settingsIndex = programSource.IndexOf("AddZaideSettings()", StringComparison.Ordinal);
        var workspaceIndex = programSource.IndexOf("AddZaideWorkspace()", StringComparison.Ordinal);
        var editorIndex = programSource.IndexOf("AddZaideEditor()", StringComparison.Ordinal);
        var terminalIndex = programSource.IndexOf("AddZaideTerminal()", StringComparison.Ordinal);
        var agentsIndex = programSource.IndexOf("AddZaideAgents()", StringComparison.Ordinal);
        var townhallIndex = programSource.IndexOf("AddZaideTownhall()", StringComparison.Ordinal);
        var sourceControlIndex = programSource.IndexOf("AddZaideSourceControl()", StringComparison.Ordinal);
        var projectSystemIndex = programSource.IndexOf("AddZaideProjectSystem()", StringComparison.Ordinal);
        Assert.True(appCoreIndex >= 0);
        Assert.True(settingsIndex > appCoreIndex);
        Assert.True(workspaceIndex > settingsIndex);
        Assert.True(editorIndex > workspaceIndex);
        Assert.True(terminalIndex > editorIndex);
        Assert.True(agentsIndex > terminalIndex);
        Assert.True(townhallIndex > agentsIndex);
        Assert.True(sourceControlIndex > townhallIndex);
        Assert.True(projectSystemIndex > sourceControlIndex);

        Assert.DoesNotContain(
            "AddSingleton<IProjectFileSystem, FileSystemProjectFileSystem>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<IProjectDiscovery, ProjectDiscovery>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<IProjectContextService, ProjectContextService>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<IProjectOperationGate, ProjectOperationGate>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<IProjectDebugTargetResolver, ProjectDebugTargetResolver>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<IProjectDebugLaunchService, ProjectDebugLaunchService>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<IManagedProcessRunner, ManagedProcessRunner>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<IProjectWorkflowService, ProjectWorkflowService>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<IProjectOutputService, ProjectOutputService>()",
            programSource);
        Assert.DoesNotContain("AddSingleton<ProjectWorkflowViewModel>()", programSource);
        Assert.DoesNotContain(
            "AddSingleton<IBuildDiagnosticsService, BuildDiagnosticsService>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<ITestResultsService, TestResultsService>()",
            programSource);
        Assert.DoesNotContain("AddSingleton<TestResultsViewModel>()", programSource);
        Assert.DoesNotContain("AddSingleton<ProblemsViewModel>()", programSource);

        // Debugging-owned and Language-owned neighbors remain direct in Program.
        Assert.Contains("AddSingleton<IDebugSessionService, DebugSessionService>()", programSource);
        Assert.Contains(
            "AddSingleton<ILanguageDiagnosticsService, LanguageDiagnosticsService>()",
            programSource);
        Assert.Contains("AddSingleton<DebugSessionViewModel>()", programSource);

        // AddLogging remains in Program (not an M6i registration).
        Assert.Contains("AddLogging(", programSource);
    }

    [Fact]
    public void ProjectSystemModuleSource_ContainsExactlyTheFourteenPlannedRegistrations()
    {
        var moduleSource = ReadRepoFile(
            "src/App/Composition/Registration/ProjectSystemServiceCollectionExtensions.cs");

        Assert.Contains(
            "internal static class ProjectSystemServiceCollectionExtensions",
            moduleSource);
        Assert.Contains(
            "internal static IServiceCollection AddZaideProjectSystem",
            moduleSource);

        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IProjectFileSystem,\s*FileSystemProjectFileSystem>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IProjectDiscovery,\s*ProjectDiscovery>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IProjectContextService,\s*ProjectContextService>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IProjectOperationGate,\s*ProjectOperationGate>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IProjectDebugTargetResolver,\s*ProjectDebugTargetResolver>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IProjectDebugLaunchService,\s*ProjectDebugLaunchService>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IManagedProcessRunner,\s*ManagedProcessRunner>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IProjectWorkflowService,\s*ProjectWorkflowService>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IProjectOutputService,\s*ProjectOutputService>\(\)"));
        Assert.Single(Regex.Matches(moduleSource, @"AddSingleton<ProjectWorkflowViewModel>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IBuildDiagnosticsService,\s*BuildDiagnosticsService>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<ITestResultsService,\s*TestResultsService>\(\)"));
        Assert.Single(Regex.Matches(moduleSource, @"AddSingleton<TestResultsViewModel>\(\)"));
        Assert.Single(Regex.Matches(moduleSource, @"AddSingleton<ProblemsViewModel>\(\)"));

        Assert.Equal(14, Regex.Matches(moduleSource, @"AddSingleton<").Count);

        // Debugging-owned and Language-owned types must not leak into this module.
        Assert.DoesNotContain("IDebugSessionService", moduleSource);
        Assert.DoesNotContain("DebugSessionViewModel", moduleSource);
        Assert.DoesNotContain("ILanguageDiagnosticsService", moduleSource);
        Assert.DoesNotContain("ILanguageSessionService", moduleSource);
    }

    [Fact]
    public void ProgramSource_StillDeclaresM6jPlusRegistrationsDirectly()
    {
        var programSource = ReadRepoFile("src/App/Composition/Program.cs");

        foreach (var marker in M6jPlusDirectMarkers)
        {
            Assert.Contains(marker, programSource);
        }

        // M6j–M6k modules do not exist yet.
        Assert.DoesNotContain("AddZaideLanguage", programSource);
        Assert.DoesNotContain("AddZaideDebugging", programSource);
    }
}
