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
using Zaide.Features.Debugging.Application;
using Zaide.Features.Debugging.Contracts;
using Zaide.Features.Debugging.Infrastructure.Dap;
using Zaide.Features.Debugging.Presentation;

namespace Zaide.Tests.App.Composition;

/// <summary>
/// Refactor 6.3 M6k: proves Debugging DI membership moved into
/// <see cref="DebuggingServiceCollectionExtensions.AddZaideDebugging"/> without
/// changing service types, lifetimes, mappings, factory behavior, or total
/// registration membership.
/// </summary>
/// <remarks>
/// <para>
/// <b>Resolution safety (constructor audit):</b>
/// </para>
/// <list type="bullet">
/// <item>
/// <see cref="DebugAdapterLocator"/> constructor only stores the optional
/// configured-path string — no PATH/file I/O and no process start until
/// <c>Resolve()</c>. Registration factory captures
/// <c>ZAIDE_NETCOREDBG_PATH</c> without calling <c>Resolve()</c>.
/// </item>
/// <item>
/// <see cref="DebugAdapterSessionFactory"/> has no constructor body —
/// netcoredbg process/DAP transport start only on <c>StartAsync</c>.
/// </item>
/// <item>
/// <see cref="DebugSessionTimeoutPolicy"/> parameterless constructor only
/// assigns production timeout constants.
/// </item>
/// <item>
/// <see cref="DebugSessionService"/> constructor stores dependencies,
/// subscribes to project-context changes, and publishes an initial snapshot.
/// With the empty production project context the snapshot is Unavailable;
/// construction does not call the locator, start netcoredbg, open DAP
/// transport, or touch the network. Process start only on
/// <c>StartLaunchAsync</c>.
/// </item>
/// <item>
/// <see cref="BreakpointService"/> constructor only stores dependencies —
/// settings mutation and workspace-root reads occur on explicit API calls,
/// not on construction.
/// </item>
/// <item>
/// Debug*ViewModel constructors store dependencies and wire ReactiveUI
/// commands/observables — no DAP session start, breakpoint mutation, or
/// process launch on construction.
/// </item>
/// </list>
/// <para>
/// All ten services are therefore resolution-tested under the empty production
/// project context. Tests never call <c>DebugAdapterLocator.Resolve()</c>,
/// <c>StartAsync</c>, <c>StartLaunchAsync</c>, or breakpoint mutation methods,
/// and do not depend on netcoredbg being installed.
/// </para>
/// </remarks>
public sealed class DebuggingRegistrationModuleTests
{
    private const string NetCoreDbgPathEnvVar = "ZAIDE_NETCOREDBG_PATH";

    private static readonly string[] DebuggingServiceTypeNames =
    {
        typeof(IDebugAdapterLocator).FullName!,
        typeof(IDebugAdapterSessionFactory).FullName!,
        typeof(DebugSessionTimeoutPolicy).FullName!,
        typeof(IDebugSessionService).FullName!,
        typeof(IBreakpointService).FullName!,
        typeof(DebugSessionViewModel).FullName!,
        typeof(DebugStackProjectionViewModel).FullName!,
        typeof(DebugCurrentLocationViewModel).FullName!,
        typeof(DebugPanelViewModel).FullName!,
        typeof(EditorBreakpointViewModel).FullName!,
    };

    private static readonly string[] DebuggingDirectRegistrationMarkers =
    {
        "AddSingleton<IDebugAdapterLocator>",
        "AddSingleton<IDebugAdapterSessionFactory, DebugAdapterSessionFactory>()",
        "AddSingleton<DebugSessionTimeoutPolicy>()",
        "AddSingleton<IDebugSessionService, DebugSessionService>()",
        "AddSingleton<IBreakpointService, BreakpointService>()",
        "AddSingleton<DebugSessionViewModel>()",
        "AddSingleton<DebugStackProjectionViewModel>()",
        "AddSingleton<DebugCurrentLocationViewModel>()",
        "AddSingleton<DebugPanelViewModel>()",
        "AddSingleton<EditorBreakpointViewModel>()",
    };

    static DebuggingRegistrationModuleTests()
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
    public void AddZaideDebugging_RegistersExactlyTenPlannedServices()
    {
        var services = new ServiceCollection();
        var returned = services.AddZaideDebugging();

        Assert.Same(services, returned);
        Assert.Equal(10, services.Count);
        Assert.All(services, d => Assert.Equal(ServiceLifetime.Singleton, d.Lifetime));

        var serviceTypes = services
            .Select(d => d.ServiceType.FullName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        var expected = DebuggingServiceTypeNames
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected, serviceTypes);

        // Locator remains factory-registered (not ImplementationType mapping).
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IDebugAdapterLocator)
                && d.ImplementationFactory is not null
                && d.ImplementationType is null);

        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IDebugAdapterSessionFactory)
                && d.ImplementationType == typeof(DebugAdapterSessionFactory));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IDebugSessionService)
                && d.ImplementationType == typeof(DebugSessionService));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IBreakpointService)
                && d.ImplementationType == typeof(BreakpointService));

        // Timeout policy and five ViewModels remain self-registrations.
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(DebugSessionTimeoutPolicy)
                && d.ImplementationType == typeof(DebugSessionTimeoutPolicy));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(DebugSessionViewModel)
                && d.ImplementationType == typeof(DebugSessionViewModel));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(DebugStackProjectionViewModel)
                && d.ImplementationType == typeof(DebugStackProjectionViewModel));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(DebugCurrentLocationViewModel)
                && d.ImplementationType == typeof(DebugCurrentLocationViewModel));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(DebugPanelViewModel)
                && d.ImplementationType == typeof(DebugPanelViewModel));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(EditorBreakpointViewModel)
                && d.ImplementationType == typeof(EditorBreakpointViewModel));
    }

    [Fact]
    public void AddZaideDebugging_LocatorFactory_CapturesCurrentZaideNetCoreDbgPathWithoutResolve()
    {
        var originalPath = Environment.GetEnvironmentVariable(NetCoreDbgPathEnvVar);
        const string testPath = "/tmp/zaide-m6k-test-netcoredbg-path";

        try
        {
            Environment.SetEnvironmentVariable(NetCoreDbgPathEnvVar, testPath);

            var services = new ServiceCollection();
            services.AddZaideDebugging();

            var descriptor = Assert.Single(
                services,
                d => d.ServiceType == typeof(IDebugAdapterLocator));
            Assert.NotNull(descriptor.ImplementationFactory);
            Assert.Null(descriptor.ImplementationType);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);

            // Invoke the registration factory only — never call Resolve().
            using var emptyProvider = new ServiceCollection().BuildServiceProvider();
            var created = descriptor.ImplementationFactory!(emptyProvider);
            var locator = Assert.IsType<DebugAdapterLocator>(created);

            var configuredPathField = typeof(DebugAdapterLocator).GetField(
                "_configuredPath",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(configuredPathField);
            var captured = configuredPathField!.GetValue(locator) as string;
            Assert.Equal(testPath, captured);
        }
        finally
        {
            Environment.SetEnvironmentVariable(NetCoreDbgPathEnvVar, originalPath);
        }
    }

    [Fact]
    public void ProgramConfigureServices_ResolvesDebuggingServicesAsSingletons()
    {
        // Resolution-tested (empty production project context — no netcoredbg start,
        // no DAP transport, no breakpoint mutation, no Resolve()): all ten services.
        // See type remarks for constructor safety proof.
        using var provider = BuildProductionProvider();

        var locator1 = provider.GetRequiredService<IDebugAdapterLocator>();
        var locator2 = provider.GetRequiredService<IDebugAdapterLocator>();
        Assert.Same(locator1, locator2);
        Assert.IsType<DebugAdapterLocator>(locator1);

        var factory1 = provider.GetRequiredService<IDebugAdapterSessionFactory>();
        var factory2 = provider.GetRequiredService<IDebugAdapterSessionFactory>();
        Assert.Same(factory1, factory2);
        Assert.IsType<DebugAdapterSessionFactory>(factory1);

        var timeout1 = provider.GetRequiredService<DebugSessionTimeoutPolicy>();
        var timeout2 = provider.GetRequiredService<DebugSessionTimeoutPolicy>();
        Assert.Same(timeout1, timeout2);

        var session1 = provider.GetRequiredService<IDebugSessionService>();
        var session2 = provider.GetRequiredService<IDebugSessionService>();
        Assert.Same(session1, session2);
        Assert.IsType<DebugSessionService>(session1);

        var breakpoints1 = provider.GetRequiredService<IBreakpointService>();
        var breakpoints2 = provider.GetRequiredService<IBreakpointService>();
        Assert.Same(breakpoints1, breakpoints2);
        Assert.IsType<BreakpointService>(breakpoints1);

        var sessionVm1 = provider.GetRequiredService<DebugSessionViewModel>();
        var sessionVm2 = provider.GetRequiredService<DebugSessionViewModel>();
        Assert.Same(sessionVm1, sessionVm2);

        var stack1 = provider.GetRequiredService<DebugStackProjectionViewModel>();
        var stack2 = provider.GetRequiredService<DebugStackProjectionViewModel>();
        Assert.Same(stack1, stack2);

        var location1 = provider.GetRequiredService<DebugCurrentLocationViewModel>();
        var location2 = provider.GetRequiredService<DebugCurrentLocationViewModel>();
        Assert.Same(location1, location2);

        var panel1 = provider.GetRequiredService<DebugPanelViewModel>();
        var panel2 = provider.GetRequiredService<DebugPanelViewModel>();
        Assert.Same(panel1, panel2);

        var editorBp1 = provider.GetRequiredService<EditorBreakpointViewModel>();
        var editorBp2 = provider.GetRequiredService<EditorBreakpointViewModel>();
        Assert.Same(editorBp1, editorBp2);
    }

    [Fact]
    public void ProgramSource_CallsAddZaideDebuggingOnce_AndDoesNotDeclareDebuggingRegistrations()
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

        foreach (var marker in DebuggingDirectRegistrationMarkers)
        {
            Assert.DoesNotContain(marker, programSource);
        }

        // No direct production AddSingleton registrations remain in Program.
        Assert.DoesNotContain("AddSingleton<", programSource);
        Assert.DoesNotContain("AddSingleton(", programSource);

        // AddLogging remains in Program (not an M6k registration).
        Assert.Contains("AddLogging(", programSource);

        // M7: CompositionRoot store assigned in Program; no fictitious registration module.
        Assert.Contains("CompositionRoot.Services = sp!", programSource);
        Assert.DoesNotContain("App.Services", programSource);
        Assert.DoesNotContain("AddZaideCompositionRoot", programSource);
        Assert.DoesNotContain("AddZaideComposition", programSource);
    }

    [Fact]
    public void DebuggingModuleSource_ContainsExactlyTheTenPlannedRegistrations()
    {
        var moduleSource = ReadRepoFile(
            "src/App/Composition/Registration/DebuggingServiceCollectionExtensions.cs");

        Assert.Contains(
            "internal static class DebuggingServiceCollectionExtensions",
            moduleSource);
        Assert.Contains(
            "internal static IServiceCollection AddZaideDebugging",
            moduleSource);

        Assert.Single(Regex.Matches(moduleSource, @"AddSingleton<IDebugAdapterLocator>"));
        Assert.Contains(
            "new DebugAdapterLocator(Environment.GetEnvironmentVariable(\"ZAIDE_NETCOREDBG_PATH\"))",
            moduleSource);
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IDebugAdapterSessionFactory,\s*DebugAdapterSessionFactory>\(\)"));
        Assert.Single(Regex.Matches(moduleSource, @"AddSingleton<DebugSessionTimeoutPolicy>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IDebugSessionService,\s*DebugSessionService>\(\)"));
        Assert.Single(
            Regex.Matches(
                moduleSource,
                @"AddSingleton<IBreakpointService,\s*BreakpointService>\(\)"));
        Assert.Single(Regex.Matches(moduleSource, @"AddSingleton<DebugSessionViewModel>\(\)"));
        Assert.Single(
            Regex.Matches(moduleSource, @"AddSingleton<DebugStackProjectionViewModel>\(\)"));
        Assert.Single(
            Regex.Matches(moduleSource, @"AddSingleton<DebugCurrentLocationViewModel>\(\)"));
        Assert.Single(Regex.Matches(moduleSource, @"AddSingleton<DebugPanelViewModel>\(\)"));
        Assert.Single(Regex.Matches(moduleSource, @"AddSingleton<EditorBreakpointViewModel>\(\)"));

        Assert.Equal(10, Regex.Matches(moduleSource, @"AddSingleton<").Count);

        // Milestone comments preserved from Program.
        Assert.Contains("Phase 12 M1", moduleSource);
        Assert.Contains("Phase 12 M2", moduleSource);

        // Locator factory does not call Resolve during registration.
        Assert.DoesNotContain(".Resolve()", moduleSource);
        Assert.DoesNotContain("StartAsync", moduleSource);
        Assert.DoesNotContain("StartLaunchAsync", moduleSource);

        // Debugging registration module remains free of composition-root store access.
        Assert.DoesNotContain("CompositionRoot", moduleSource);
    }

    [Fact]
    public void ProgramAndAppSource_UseCompositionRootServices_AndPublicAppServicesRemoved()
    {
        var programSource = ReadRepoFile("src/App/Composition/Program.cs");
        var appSource = ReadRepoFile("src/App/Composition/App.axaml.cs");
        var compositionRootSource = ReadRepoFile("src/App/Composition/CompositionRoot.cs");

        Assert.Contains("CompositionRoot.Services = sp!", programSource);
        Assert.DoesNotContain("App.Services", programSource);
        Assert.DoesNotContain("public static IServiceProvider Services", appSource);
        Assert.Contains("CompositionRoot.Services", appSource);
        Assert.Contains("DisposeServicesOnExit", appSource);
        Assert.Contains("ApplicationShutdown.Run", appSource);
        Assert.Contains("internal static class CompositionRoot", compositionRootSource);
        Assert.Contains("internal static IServiceProvider Services { get; set; }", compositionRootSource);
        Assert.DoesNotContain("GetRequiredService", compositionRootSource);
        Assert.DoesNotContain("GetService", compositionRootSource);
    }
}
