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
using Zaide.Features.Settings.Presentation;

namespace Zaide.Tests.App.Composition;

/// <summary>
/// Refactor 6.3 M6b / M10: proves Settings DI membership in
/// <see cref="SettingsServiceCollectionExtensions.AddZaideSettings"/> —
/// service types, lifetimes, secret-path factory, and the M10 panel factory.
/// </summary>
public sealed class SettingsRegistrationModuleTests
{
    private static readonly string[] SettingsServiceTypeNames =
    {
        typeof(ISettingsService).FullName!,
        typeof(ISecretStore).FullName!,
        typeof(ISettingsPanelFactory).FullName!,
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
    public void AddZaideSettings_RegistersExactlyThreePlannedServices()
    {
        var services = new ServiceCollection();
        var returned = services.AddZaideSettings();

        Assert.Same(services, returned);
        Assert.Equal(3, services.Count);
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
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(ISettingsPanelFactory)
                && d.ImplementationType == typeof(SettingsPanelFactory));
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

        var factory1 = provider.GetRequiredService<ISettingsPanelFactory>();
        var factory2 = provider.GetRequiredService<ISettingsPanelFactory>();
        Assert.Same(factory1, factory2);
        Assert.IsType<SettingsPanelFactory>(factory1);
    }

    [Fact]
    public void ProgramSource_CallsAddZaideSettingsOnce_AndDoesNotDeclareSettingsRegistrations()
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
            "AddSingleton<ISettingsService, SettingsService>()",
            programSource);
        Assert.DoesNotContain("AddSingleton<ISecretStore>", programSource);
        Assert.DoesNotContain("FileSecretStore", programSource);
        Assert.DoesNotContain("SettingsPathResolver", programSource);

        // AddLogging remains in Program (not an M6b registration).
        Assert.Contains("AddLogging(", programSource);
    }

    [Fact]
    public void SettingsModuleSource_ContainsExactlyTheThreePlannedRegistrations()
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
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<ISettingsPanelFactory,\s*SettingsPanelFactory>\(\)"));

        Assert.Equal(3, Regex.Matches(moduleSource, @"AddSingleton<").Count);
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
