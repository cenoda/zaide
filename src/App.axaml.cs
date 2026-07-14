using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using System;
using Zaide.Services;
using Zaide.ViewModels;

namespace Zaide;

public partial class App : Application
{
    public static IServiceProvider Services { get; set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = Services.GetRequiredService<MainWindowViewModel>();
            var settings = Services.GetRequiredService<ISettingsService>();
            var secrets = Services.GetRequiredService<ISecretStore>();
            var registry = Services.GetRequiredService<ICommandRegistry>();
            var statusBar = Services.GetRequiredService<StatusBarViewModel>();

            // Phase 9 M1: eagerly resolve the palette VM so it registers
            // palette.open in the ICommandRegistry singleton before
            // MainWindow.MaterializeRegistryBindings() materialises Ctrl+Shift+P.
            var paletteVm = Services.GetRequiredService<CommandPaletteViewModel>();
            var searchVm = Services.GetRequiredService<EditorSearchViewModel>();
            var languageInputVm = Services.GetRequiredService<EditorLanguageInputViewModel>();

            // Phase 10 M2: eagerly resolve the document bridge so Workspace/session
            // subscriptions start before editors open files.
            _ = Services.GetRequiredService<ILanguageDocumentBridge>();
            // Phase 10 M3: resolve diagnostics ownership after the document bridge.
            _ = Services.GetRequiredService<ILanguageDiagnosticsService>();
            // Phase 10 M4: completion/hover services before editors open.
            _ = Services.GetRequiredService<ILanguageCompletionService>();
            _ = Services.GetRequiredService<ILanguageHoverService>();
            // Phase 10 M5: definition/symbol services before editors open.
            _ = Services.GetRequiredService<ILanguageNavigationService>();
            _ = Services.GetRequiredService<ILanguageSymbolService>();
            // Phase 10 M6: formatting service before editors open.
            _ = Services.GetRequiredService<ILanguageFormattingService>();

            desktop.MainWindow = new MainWindow(settings, secrets, registry, statusBar, paletteVm, searchVm, languageInputVm) { ViewModel = vm };

            // Dispose the terminal host on exit so the active session's shell
            // process is killed and doesn't outlive the app.
            desktop.Exit += (_, _) => DisposeServicesOnExit(Services);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Explicit application-shutdown dispose sequence. Extracted for unit tests
    /// that verify ordering without a live Avalonia desktop host.
    /// </summary>
    internal static void DisposeServicesOnExit(IServiceProvider services)
    {
        // Phase 11 F10: resolve projection singletons before workflow dispose so
        // lazy DI never constructs them against completed workflow subjects.
        var output = services.GetRequiredService<IProjectOutputService>();
        var buildDiagnostics = services.GetRequiredService<IBuildDiagnosticsService>();
        var testResults = services.GetRequiredService<ITestResultsService>();

        // Phase 12 M1: disconnect the debug adapter before workflow teardown so
        // adapter/debuggee process trees are never orphaned.
        services.GetRequiredService<IDebugSessionService>().Dispose();

        // Phase 11 M1: cancel and kill workflow dotnet trees before language
        // session teardown so child processes are never orphaned.
        services.GetRequiredService<IProjectWorkflowService>().Dispose();

        // Release workflow subscriptions and complete projection subjects after
        // process kill. Language teardown stays after both.
        output.Dispose();
        buildDiagnostics.Dispose();
        testResults.Dispose();

        // Phase 8.3 M3: explicit shutdown of the project-context service
        // so its WorkspaceFolderChanged subscription is released and any
        // in-flight work is invalidated. App does not rely on implicit
        // root-provider disposal.
        // Tear down language features before document sync/session teardown.
        services.GetRequiredService<ILanguageFormattingService>().Dispose();
        services.GetRequiredService<ILanguageNavigationService>().Dispose();
        services.GetRequiredService<ILanguageSymbolService>().Dispose();
        services.GetRequiredService<ILanguageCompletionService>().Dispose();
        services.GetRequiredService<ILanguageHoverService>().Dispose();
        services.GetRequiredService<ILanguageDiagnosticsService>().Dispose();
        services.GetRequiredService<ILanguageDocumentBridge>().Dispose();
        services.GetRequiredService<ILanguageSessionService>().Dispose();
        services.GetRequiredService<IProjectContextService>().Dispose();
        services.GetService<ITerminalHost>()?.Dispose();
    }
}
