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
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Infrastructure;
using Zaide.Features.Editor.Presentation;

namespace Zaide.Tests.App.Composition;

/// <summary>
/// Refactor 6.3 M6d: proves Editor DI membership moved into
/// <see cref="EditorServiceCollectionExtensions.AddZaideEditor"/> without
/// changing service types, lifetimes, mappings, or total registration
/// membership. <see cref="EditorViewModel"/> remains unregistered.
/// </summary>
public sealed class EditorRegistrationModuleTests
{
    private static readonly string[] EditorServiceTypeNames =
    {
        typeof(IFileService).FullName!,
        typeof(IEditorSessionFactory).FullName!,
        typeof(IEditorReadOnlyTabService).FullName!,
        typeof(EditorSearchViewModel).FullName!,
        typeof(EditorTabViewModel).FullName!,
        typeof(EditorLanguageInputViewModel).FullName!,
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

    static EditorRegistrationModuleTests()
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
    public void AddZaideEditor_RegistersExactlySixPlannedServices()
    {
        var services = new ServiceCollection();
        services.AddZaideEditor();

        Assert.Equal(6, services.Count);
        Assert.All(services, d => Assert.Equal(ServiceLifetime.Singleton, d.Lifetime));

        var serviceTypes = services
            .Select(d => d.ServiceType.FullName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        var expected = EditorServiceTypeNames
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected, serviceTypes);

        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IFileService)
                && d.ImplementationType == typeof(FileService));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IEditorSessionFactory)
                && d.ImplementationType == typeof(EditorSessionFactory));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IEditorReadOnlyTabService)
                && d.ImplementationType == typeof(EditorReadOnlyTabService));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(EditorSearchViewModel)
                && d.ImplementationType == typeof(EditorSearchViewModel));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(EditorTabViewModel)
                && d.ImplementationType == typeof(EditorTabViewModel));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(EditorLanguageInputViewModel)
                && d.ImplementationType == typeof(EditorLanguageInputViewModel));

        // Locked exclusion: EditorViewModel is factory-created, not DI-registered.
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(EditorViewModel));
    }

    [Fact]
    public void ProgramConfigureServices_ResolvesEditorServicesAsSingletons()
    {
        using var provider = BuildProductionProvider();

        var file1 = provider.GetRequiredService<IFileService>();
        var file2 = provider.GetRequiredService<IFileService>();
        Assert.Same(file1, file2);
        Assert.IsType<FileService>(file1);

        var sessionFactory1 = provider.GetRequiredService<IEditorSessionFactory>();
        var sessionFactory2 = provider.GetRequiredService<IEditorSessionFactory>();
        Assert.Same(sessionFactory1, sessionFactory2);
        Assert.IsType<EditorSessionFactory>(sessionFactory1);

        var readOnly1 = provider.GetRequiredService<IEditorReadOnlyTabService>();
        var readOnly2 = provider.GetRequiredService<IEditorReadOnlyTabService>();
        Assert.Same(readOnly1, readOnly2);
        Assert.IsType<EditorReadOnlyTabService>(readOnly1);

        var search1 = provider.GetRequiredService<EditorSearchViewModel>();
        var search2 = provider.GetRequiredService<EditorSearchViewModel>();
        Assert.Same(search1, search2);

        var tabs1 = provider.GetRequiredService<EditorTabViewModel>();
        var tabs2 = provider.GetRequiredService<EditorTabViewModel>();
        Assert.Same(tabs1, tabs2);

        var language1 = provider.GetRequiredService<EditorLanguageInputViewModel>();
        var language2 = provider.GetRequiredService<EditorLanguageInputViewModel>();
        Assert.Same(language1, language2);
    }

    [Fact]
    public void ProgramConfigureServices_DoesNotRegisterEditorViewModel()
    {
        var services = new ServiceCollection();
        Program.ConfigureServices(services);

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(EditorViewModel));
    }

    [Fact]
    public void ProgramSource_CallsAddZaideEditorOnce_AndDoesNotDeclareEditorRegistrations()
    {
        var programSource = ReadRepoFile("src/App/Composition/Program.cs");

        Assert.Single(Regex.Matches(programSource, @"AddZaideAppCore\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideSettings\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideWorkspace\s*\(\s*\)"));
        Assert.Single(Regex.Matches(programSource, @"AddZaideEditor\s*\(\s*\)"));

        var appCoreIndex = programSource.IndexOf("AddZaideAppCore()", StringComparison.Ordinal);
        var settingsIndex = programSource.IndexOf("AddZaideSettings()", StringComparison.Ordinal);
        var workspaceIndex = programSource.IndexOf("AddZaideWorkspace()", StringComparison.Ordinal);
        var editorIndex = programSource.IndexOf("AddZaideEditor()", StringComparison.Ordinal);
        Assert.True(appCoreIndex >= 0);
        Assert.True(settingsIndex > appCoreIndex);
        Assert.True(workspaceIndex > settingsIndex);
        Assert.True(editorIndex > workspaceIndex);

        Assert.DoesNotContain(
            "AddSingleton<IFileService, FileService>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<IEditorSessionFactory, EditorSessionFactory>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<IEditorReadOnlyTabService, EditorReadOnlyTabService>()",
            programSource);
        Assert.DoesNotContain("AddSingleton<EditorSearchViewModel>()", programSource);
        Assert.DoesNotContain("AddSingleton<EditorTabViewModel>()", programSource);
        Assert.DoesNotContain("AddSingleton<EditorLanguageInputViewModel>()", programSource);
        Assert.DoesNotContain("AddTransient<EditorViewModel>", programSource);
        Assert.DoesNotContain("AddSingleton<EditorViewModel>", programSource);

        // AddLogging remains in Program (not an M6d registration).
        Assert.Contains("AddLogging(", programSource);
    }

    [Fact]
    public void EditorModuleSource_ContainsExactlyTheSixPlannedRegistrations()
    {
        var moduleSource = ReadRepoFile(
            "src/App/Composition/Registration/EditorServiceCollectionExtensions.cs");

        Assert.Contains(
            "internal static class EditorServiceCollectionExtensions",
            moduleSource);
        Assert.Contains("internal static IServiceCollection AddZaideEditor", moduleSource);

        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IFileService,\s*FileService>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IEditorSessionFactory,\s*EditorSessionFactory>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IEditorReadOnlyTabService,\s*EditorReadOnlyTabService>\(\)"));
        Assert.Single(Regex.Matches(moduleSource, @"AddSingleton<EditorSearchViewModel>\(\)"));
        Assert.Single(Regex.Matches(moduleSource, @"AddSingleton<EditorTabViewModel>\(\)"));
        Assert.Single(
            Regex.Matches(moduleSource, @"AddSingleton<EditorLanguageInputViewModel>\(\)"));

        Assert.Equal(6, Regex.Matches(moduleSource, @"AddSingleton<").Count);

        // Locked exclusion: EditorViewModel must not be registered.
        Assert.DoesNotContain("EditorViewModel", moduleSource);
    }

    [Fact]
    public void ProgramSource_StillDeclaresM6ePlusRegistrationsDirectly()
    {
        var programSource = ReadRepoFile("src/App/Composition/Program.cs");

        foreach (var marker in M6ePlusDirectMarkers)
        {
            Assert.Contains(marker, programSource);
        }

        // M6e–M6k modules do not exist yet.
        Assert.DoesNotContain("AddZaideTerminal", programSource);
        Assert.DoesNotContain("AddZaideAgents", programSource);
        Assert.DoesNotContain("AddZaideTownhall", programSource);
        Assert.DoesNotContain("AddZaideSourceControl", programSource);
        Assert.DoesNotContain("AddZaideProjectSystem", programSource);
        Assert.DoesNotContain("AddZaideLanguage", programSource);
        Assert.DoesNotContain("AddZaideDebugging", programSource);
    }
}
