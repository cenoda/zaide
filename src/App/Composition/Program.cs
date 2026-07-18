using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Avalonia.Splat;
using System;
using Zaide.App.Composition.Registration;
using Zaide.Features.Debugging.Infrastructure.Dap;
using Zaide.Features.Language.Infrastructure.Lsp;
using Zaide.Features.Language.Contracts;
using Zaide.Features.Language.Application;
using Zaide.Features.Debugging.Contracts;
using Zaide.Features.Debugging.Application;
using Zaide.Features.Debugging.Presentation;

namespace Zaide.App.Composition;
class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    /// <summary>
    /// Registers all production services in the dependency-injection container.
    /// Extracted from <see cref="BuildAvaloniaApp"/> so unit tests can build an
    /// identical container and verify resolution without re-declaring every
    /// registration.
    /// </summary>
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddZaideAppCore();
        services.AddZaideSettings();
        services.AddZaideWorkspace();
        services.AddZaideEditor();
        services.AddZaideTerminal();
        services.AddZaideAgents();
        services.AddZaideTownhall();
        services.AddZaideSourceControl();
        services.AddZaideProjectSystem();

        services.AddLogging(builder => builder.AddConsole());

        // Phase 12 M1: UI-independent DAP adapter locator and session lifecycle core.
        services.AddSingleton<IDebugAdapterLocator>(_ =>
            new DebugAdapterLocator(Environment.GetEnvironmentVariable("ZAIDE_NETCOREDBG_PATH")));
        services.AddSingleton<IDebugAdapterSessionFactory, DebugAdapterSessionFactory>();
        services.AddSingleton<DebugSessionTimeoutPolicy>();
        services.AddSingleton<IDebugSessionService, DebugSessionService>();

        // Phase 12 M2: workspace-scoped persistent breakpoint storage.
        services.AddSingleton<IBreakpointService, BreakpointService>();

        services.AddSingleton<DebugSessionViewModel>();
        services.AddSingleton<DebugStackProjectionViewModel>();
        services.AddSingleton<DebugCurrentLocationViewModel>();
        services.AddSingleton<DebugPanelViewModel>();
        services.AddSingleton<EditorBreakpointViewModel>();

        // Phase 10 M1: C# language session (process + StreamJsonRpc transport).
        services.AddSingleton<ILanguageServerBinaryLocator, LanguageServerBinaryLocator>();
        services.AddSingleton<ILanguageServerSessionFactory, CsharpLsSessionFactory>();
        services.AddSingleton<ILanguageSessionService, LanguageSessionService>();
        services.AddSingleton<ILanguageDocumentBridge, LanguageDocumentBridge>();

        // Phase 10 M3: structured diagnostics + Problems projection.
        services.AddSingleton<ILanguageDiagnosticsService, LanguageDiagnosticsService>();

        // Phase 10 M4: active-document completion and hover.
        services.AddSingleton<ILanguageCompletionService, LanguageCompletionService>();
        services.AddSingleton<ILanguageHoverService, LanguageHoverService>();

        // Phase 10 M5: Go to Definition + document/workspace symbols.
        services.AddSingleton<ILanguageNavigationService, LanguageNavigationService>();
        services.AddSingleton<ILanguageSymbolService, LanguageSymbolService>();

        // Phase 10 M6: whole-document formatting + Format on Save coordination.
        services.AddSingleton<ILanguageFormattingService, LanguageFormattingService>();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseReactiveUIWithMicrosoftDependencyResolver(
                containerConfig: ConfigureServices,
                withResolver: sp =>
                {
                    App.Services = sp!;
                })
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
