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
using Zaide.Features.Language.Application;
using Zaide.Features.Language.Contracts;
using Zaide.Features.Language.Infrastructure.Lsp;
using Zaide.Features.ProjectSystem.Presentation;

namespace Zaide.Tests.App.Composition;

/// <summary>
/// Refactor 6.3 M6j: proves Language DI membership moved into
/// <see cref="LanguageServiceCollectionExtensions.AddZaideLanguage"/> without
/// changing service types, lifetimes, mappings, or total registration membership.
/// </summary>
/// <remarks>
/// <para>
/// <b>Resolution safety (constructor audit):</b>
/// </para>
/// <list type="bullet">
/// <item>
/// <see cref="LanguageServerBinaryLocator"/> constructor only stores an optional
/// path string — no PATH/file I/O until <c>Resolve()</c>.
/// </item>
/// <item>
/// <see cref="CsharpLsSessionFactory"/> has no constructor body — csharp-ls
/// process/transport start only on <c>StartAsync</c>.
/// </item>
/// <item>
/// <see cref="LanguageSessionService"/> constructor subscribes to project-context
/// changes and schedules <c>ReconcileAsync</c>, which starts csharp-ls only when
/// the context is eligible (<c>SelectedProject</c> non-null). Default production
/// DI yields an empty project context, so construction does not start a language
/// server, open transport, or touch the network.
/// </item>
/// <item>
/// Document bridge / diagnostics / completion / hover / navigation / symbol /
/// formatting constructors only store dependencies and subscribe to workspace
/// or session observables — no process start or external I/O on construction.
/// </item>
/// </list>
/// <para>
/// All ten services are therefore resolution-tested under the empty production
/// project context. Tests never call language-server APIs that would launch
/// csharp-ls, open transport, or depend on a locally installed language server.
/// </para>
/// </remarks>
public sealed class LanguageRegistrationModuleTests
{
    private static readonly string[] LanguageServiceTypeNames =
    {
        typeof(ILanguageServerBinaryLocator).FullName!,
        typeof(ILanguageServerSessionFactory).FullName!,
        typeof(ILanguageSessionService).FullName!,
        typeof(ILanguageDocumentBridge).FullName!,
        typeof(ILanguageDiagnosticsService).FullName!,
        typeof(ILanguageCompletionService).FullName!,
        typeof(ILanguageHoverService).FullName!,
        typeof(ILanguageNavigationService).FullName!,
        typeof(ILanguageSymbolService).FullName!,
        typeof(ILanguageFormattingService).FullName!,
    };


    static LanguageRegistrationModuleTests()
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
    public void AddZaideLanguage_RegistersExactlyTenPlannedServices()
    {
        var services = new ServiceCollection();
        var returned = services.AddZaideLanguage();

        Assert.Same(services, returned);
        Assert.Equal(10, services.Count);
        Assert.All(services, d => Assert.Equal(ServiceLifetime.Singleton, d.Lifetime));

        var serviceTypes = services
            .Select(d => d.ServiceType.FullName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        var expected = LanguageServiceTypeNames
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected, serviceTypes);

        // Ten interface-to-implementation mappings remain unchanged.
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(ILanguageServerBinaryLocator)
                && d.ImplementationType == typeof(LanguageServerBinaryLocator));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(ILanguageServerSessionFactory)
                && d.ImplementationType == typeof(CsharpLsSessionFactory));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(ILanguageSessionService)
                && d.ImplementationType == typeof(LanguageSessionService));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(ILanguageDocumentBridge)
                && d.ImplementationType == typeof(LanguageDocumentBridge));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(ILanguageDiagnosticsService)
                && d.ImplementationType == typeof(LanguageDiagnosticsService));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(ILanguageCompletionService)
                && d.ImplementationType == typeof(LanguageCompletionService));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(ILanguageHoverService)
                && d.ImplementationType == typeof(LanguageHoverService));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(ILanguageNavigationService)
                && d.ImplementationType == typeof(LanguageNavigationService));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(ILanguageSymbolService)
                && d.ImplementationType == typeof(LanguageSymbolService));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(ILanguageFormattingService)
                && d.ImplementationType == typeof(LanguageFormattingService));

        // ProblemsViewModel is ProjectSystem-owned (M6i), not Language-owned.
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(ProblemsViewModel));
    }

    [Fact]
    public void ProgramConfigureServices_ResolvesLanguageServicesAsSingletons()
    {
        // Resolution-tested (empty production project context — no csharp-ls start):
        // all ten Language services. See type remarks for constructor safety proof.
        using var provider = BuildProductionProvider();

        var locator1 = provider.GetRequiredService<ILanguageServerBinaryLocator>();
        var locator2 = provider.GetRequiredService<ILanguageServerBinaryLocator>();
        Assert.Same(locator1, locator2);
        Assert.IsType<LanguageServerBinaryLocator>(locator1);

        var factory1 = provider.GetRequiredService<ILanguageServerSessionFactory>();
        var factory2 = provider.GetRequiredService<ILanguageServerSessionFactory>();
        Assert.Same(factory1, factory2);
        Assert.IsType<CsharpLsSessionFactory>(factory1);

        var session1 = provider.GetRequiredService<ILanguageSessionService>();
        var session2 = provider.GetRequiredService<ILanguageSessionService>();
        Assert.Same(session1, session2);
        Assert.IsType<LanguageSessionService>(session1);

        var bridge1 = provider.GetRequiredService<ILanguageDocumentBridge>();
        var bridge2 = provider.GetRequiredService<ILanguageDocumentBridge>();
        Assert.Same(bridge1, bridge2);
        Assert.IsType<LanguageDocumentBridge>(bridge1);

        var diagnostics1 = provider.GetRequiredService<ILanguageDiagnosticsService>();
        var diagnostics2 = provider.GetRequiredService<ILanguageDiagnosticsService>();
        Assert.Same(diagnostics1, diagnostics2);
        Assert.IsType<LanguageDiagnosticsService>(diagnostics1);

        var completion1 = provider.GetRequiredService<ILanguageCompletionService>();
        var completion2 = provider.GetRequiredService<ILanguageCompletionService>();
        Assert.Same(completion1, completion2);
        Assert.IsType<LanguageCompletionService>(completion1);

        var hover1 = provider.GetRequiredService<ILanguageHoverService>();
        var hover2 = provider.GetRequiredService<ILanguageHoverService>();
        Assert.Same(hover1, hover2);
        Assert.IsType<LanguageHoverService>(hover1);

        var navigation1 = provider.GetRequiredService<ILanguageNavigationService>();
        var navigation2 = provider.GetRequiredService<ILanguageNavigationService>();
        Assert.Same(navigation1, navigation2);
        Assert.IsType<LanguageNavigationService>(navigation1);

        var symbol1 = provider.GetRequiredService<ILanguageSymbolService>();
        var symbol2 = provider.GetRequiredService<ILanguageSymbolService>();
        Assert.Same(symbol1, symbol2);
        Assert.IsType<LanguageSymbolService>(symbol1);

        var formatting1 = provider.GetRequiredService<ILanguageFormattingService>();
        var formatting2 = provider.GetRequiredService<ILanguageFormattingService>();
        Assert.Same(formatting1, formatting2);
        Assert.IsType<LanguageFormattingService>(formatting1);

        // ProblemsViewModel remains owned by AddZaideProjectSystem (M6i).
        var problems1 = provider.GetRequiredService<ProblemsViewModel>();
        var problems2 = provider.GetRequiredService<ProblemsViewModel>();
        Assert.Same(problems1, problems2);
    }

    [Fact]
    public void ProgramSource_CallsAddZaideLanguageOnce_AndDoesNotDeclareLanguageRegistrations()
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

        Assert.DoesNotContain(
            "AddSingleton<ILanguageServerBinaryLocator, LanguageServerBinaryLocator>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<ILanguageServerSessionFactory, CsharpLsSessionFactory>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<ILanguageSessionService, LanguageSessionService>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<ILanguageDocumentBridge, LanguageDocumentBridge>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<ILanguageDiagnosticsService, LanguageDiagnosticsService>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<ILanguageCompletionService, LanguageCompletionService>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<ILanguageHoverService, LanguageHoverService>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<ILanguageNavigationService, LanguageNavigationService>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<ILanguageSymbolService, LanguageSymbolService>()",
            programSource);
        Assert.DoesNotContain(
            "AddSingleton<ILanguageFormattingService, LanguageFormattingService>()",
            programSource);

        // M6k moved Debugging registrations out of Program.
        Assert.DoesNotContain(
            "AddSingleton<IDebugSessionService, DebugSessionService>()",
            programSource);
        Assert.DoesNotContain("AddSingleton<DebugSessionViewModel>()", programSource);
        Assert.DoesNotContain("AddSingleton<", programSource);

        // AddLogging remains in Program (not an M6j registration).
        Assert.Contains("AddLogging(", programSource);
    }

    [Fact]
    public void LanguageModuleSource_ContainsExactlyTheTenPlannedRegistrations()
    {
        var moduleSource = ReadRepoFile(
            "src/App/Composition/Registration/LanguageServiceCollectionExtensions.cs");

        Assert.Contains(
            "internal static class LanguageServiceCollectionExtensions",
            moduleSource);
        Assert.Contains(
            "internal static IServiceCollection AddZaideLanguage",
            moduleSource);

        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<ILanguageServerBinaryLocator,\s*LanguageServerBinaryLocator>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<ILanguageServerSessionFactory,\s*CsharpLsSessionFactory>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<ILanguageSessionService,\s*LanguageSessionService>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<ILanguageDocumentBridge,\s*LanguageDocumentBridge>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<ILanguageDiagnosticsService,\s*LanguageDiagnosticsService>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<ILanguageCompletionService,\s*LanguageCompletionService>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<ILanguageHoverService,\s*LanguageHoverService>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<ILanguageNavigationService,\s*LanguageNavigationService>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<ILanguageSymbolService,\s*LanguageSymbolService>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<ILanguageFormattingService,\s*LanguageFormattingService>\(\)"));

        Assert.Equal(10, Regex.Matches(moduleSource, @"AddSingleton<").Count);

        // Milestone comments preserved from Program.
        Assert.Contains("Phase 10 M1", moduleSource);
        Assert.Contains("Phase 10 M3", moduleSource);
        Assert.Contains("Phase 10 M4", moduleSource);
        Assert.Contains("Phase 10 M5", moduleSource);
        Assert.Contains("Phase 10 M6", moduleSource);

        // ProblemsViewModel and Debugging-owned types must not leak into this module.
        Assert.DoesNotContain("ProblemsViewModel", moduleSource);
        Assert.DoesNotContain("IDebugSessionService", moduleSource);
        Assert.DoesNotContain("DebugSessionViewModel", moduleSource);
        Assert.DoesNotContain("IBreakpointService", moduleSource);
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

    [Fact]
    public void ProblemsViewModel_RemainsOwnedByProjectSystemModule()
    {
        var projectSystemSource = ReadRepoFile(
            "src/App/Composition/Registration/ProjectSystemServiceCollectionExtensions.cs");
        var languageSource = ReadRepoFile(
            "src/App/Composition/Registration/LanguageServiceCollectionExtensions.cs");

        Assert.Contains("AddSingleton<ProblemsViewModel>()", projectSystemSource);
        Assert.DoesNotContain("ProblemsViewModel", languageSource);
    }
}
