using System;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.Builder;
using Xunit;
using Zaide;
using Zaide.App.Composition;
using Zaide.App.Composition.Registration;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Infrastructure;

namespace Zaide.Tests.App.Composition;

/// <summary>
/// Refactor 6.3 M6b: proves Settings DI membership moved into
/// <see cref="SettingsServiceCollectionExtensions.AddZaideSettings"/> without
/// changing service types, lifetimes, secret-path factory, or total registration
/// membership.
/// </summary>
public sealed class SettingsRegistrationModuleTests
{
    private static readonly string[] SettingsServiceTypeNames =
    {
        typeof(ISettingsService).FullName!,
        typeof(ISecretStore).FullName!,
    };


    static SettingsRegistrationModuleTests()
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
    public void AddZaideSettings_RegistersExactlyTwoPlannedServices()
    {
        var services = new ServiceCollection();
        services.AddZaideSettings();

        Assert.Equal(2, services.Count);
        Assert.All(services, d => Assert.Equal(ServiceLifetime.Singleton, d.Lifetime));

        var serviceTypes = services
            .Select(d => d.ServiceType.FullName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        var expected = SettingsServiceTypeNames
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected, serviceTypes);

        Assert.Contains(
            services,
            d => d.ServiceType == typeof(ISettingsService)
                && d.ImplementationType == typeof(SettingsService));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(ISecretStore)
                && d.ImplementationFactory is not null
                && d.ImplementationType is null);
    }

    [Fact]
    public void AddZaideSettings_SecretStoreFactory_ResolvesFileSecretStoreAtProductionPath()
    {
        var services = new ServiceCollection();
        services.AddZaideSettings();

        using var provider = services.BuildServiceProvider();
        var store1 = provider.GetRequiredService<ISecretStore>();
        var store2 = provider.GetRequiredService<ISecretStore>();

        Assert.Same(store1, store2);
        var fileStore = Assert.IsType<FileSecretStore>(store1);

        var pathField = typeof(FileSecretStore).GetField(
            "_secretsPath",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(pathField);
        var actualPath = Assert.IsType<string>(pathField!.GetValue(fileStore));
        Assert.Equal(SettingsPathResolver.GetSecretsPath(), actualPath);
    }

    [Fact]
    public void ProgramConfigureServices_ResolvesSettingsServicesAsSingletons()
    {
        using var provider = BuildProductionProvider();

        var settings1 = provider.GetRequiredService<ISettingsService>();
        var settings2 = provider.GetRequiredService<ISettingsService>();
        Assert.Same(settings1, settings2);
        Assert.IsType<SettingsService>(settings1);

        var secrets1 = provider.GetRequiredService<ISecretStore>();
        var secrets2 = provider.GetRequiredService<ISecretStore>();
        Assert.Same(secrets1, secrets2);
        Assert.IsType<FileSecretStore>(secrets1);
    }

    [Fact]
    public void ProgramSource_CallsAddZaideSettingsOnce_AndDoesNotDeclareSettingsRegistrations()
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
            "AddSingleton<ISettingsService, SettingsService>()",
            programSource);
        Assert.DoesNotContain("AddSingleton<ISecretStore>", programSource);
        Assert.DoesNotContain("FileSecretStore", programSource);
        Assert.DoesNotContain("SettingsPathResolver", programSource);

        // AddLogging remains in Program (not an M6b registration).
        Assert.Contains("AddLogging(", programSource);
    }

    [Fact]
    public void SettingsModuleSource_ContainsExactlyTheTwoPlannedRegistrations()
    {
        var moduleSource = ReadRepoFile(
            "src/App/Composition/Registration/SettingsServiceCollectionExtensions.cs");

        Assert.Contains(
            "internal static class SettingsServiceCollectionExtensions",
            moduleSource);
        Assert.Contains("internal static IServiceCollection AddZaideSettings", moduleSource);

        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<ISettingsService,\s*SettingsService>\(\)"));
        Assert.Single(Regex.Matches(moduleSource, @"AddSingleton<ISecretStore>"));
        Assert.Contains(
            "new FileSecretStore(SettingsPathResolver.GetSecretsPath())",
            moduleSource);

        Assert.Equal(2, Regex.Matches(moduleSource, @"AddSingleton<").Count);

        // M10 reservation: panel factory is not registered in M6b.
        Assert.DoesNotContain("ISettingsPanelFactory", moduleSource);
        Assert.DoesNotContain("SettingsPanelFactory", moduleSource);
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
