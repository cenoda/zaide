using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using System;
using Zaide.App.Shell;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Workspace.Contracts;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.Language.Contracts;
using Zaide.Features.Debugging.Contracts;
using Zaide.Features.Debugging.Presentation;
using Zaide.Features.Terminal.Contracts;
using Zaide.Features.Terminal.Infrastructure;
using Zaide.Features.Terminal.Presentation;

namespace Zaide.App.Composition;
public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = CompositionRoot.Services.GetRequiredService<MainWindowViewModel>();
            var settings = CompositionRoot.Services.GetRequiredService<ISettingsService>();
            var secrets = CompositionRoot.Services.GetRequiredService<ISecretStore>();
            var registry = CompositionRoot.Services.GetRequiredService<ICommandRegistry>();
            var statusBar = CompositionRoot.Services.GetRequiredService<StatusBarViewModel>();

            // Phase 9 M1: eagerly resolve the palette VM so it registers
            // palette.open in the ICommandRegistry singleton before
            // MainWindow.MaterializeRegistryBindings() materialises Ctrl+Shift+P.
            var paletteVm = CompositionRoot.Services.GetRequiredService<CommandPaletteViewModel>();
            var searchVm = CompositionRoot.Services.GetRequiredService<EditorSearchViewModel>();
            var languageInputVm = CompositionRoot.Services.GetRequiredService<EditorLanguageInputViewModel>();

            // Phase 12 M3a: eagerly resolve debug commands so F5 materializes before MainWindow opens.
            _ = CompositionRoot.Services.GetRequiredService<DebugSessionViewModel>();
            var editorBreakpointVm = CompositionRoot.Services.GetRequiredService<EditorBreakpointViewModel>();
            var debugCurrentLocationVm = CompositionRoot.Services.GetRequiredService<DebugCurrentLocationViewModel>();

            // Phase 10 M2: eagerly resolve the document bridge so Workspace/session
            // subscriptions start before editors open files.
            _ = CompositionRoot.Services.GetRequiredService<ILanguageDocumentBridge>();
            // Phase 10 M3: resolve diagnostics ownership after the document bridge.
            _ = CompositionRoot.Services.GetRequiredService<ILanguageDiagnosticsService>();
            // Phase 10 M4: completion/hover services before editors open.
            _ = CompositionRoot.Services.GetRequiredService<ILanguageCompletionService>();
            _ = CompositionRoot.Services.GetRequiredService<ILanguageHoverService>();
            // Phase 10 M5: definition/symbol services before editors open.
            _ = CompositionRoot.Services.GetRequiredService<ILanguageNavigationService>();
            _ = CompositionRoot.Services.GetRequiredService<ILanguageSymbolService>();
            // Phase 10 M6: formatting service before editors open.
            _ = CompositionRoot.Services.GetRequiredService<ILanguageFormattingService>();

            desktop.MainWindow = new MainWindow(
                settings,
                secrets,
                registry,
                statusBar,
                paletteVm,
                searchVm,
                languageInputVm,
                editorBreakpointVm,
                debugCurrentLocationVm)
            {
                ViewModel = vm,
            };

            // Dispose the terminal host on exit so the active session's shell
            // process is killed and doesn't outlive the app.
            desktop.Exit += (_, _) => DisposeServicesOnExit(CompositionRoot.Services);
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

        // Phase 12 F4: tear down debug projection singletons after session
        // disconnect and before workflow disposal (Contract 3 ordering).
        DisposeDebugProjection<DebugPanelViewModel>(services);
        DisposeDebugProjection<DebugCurrentLocationViewModel>(services);
        DisposeDebugProjection<EditorBreakpointViewModel>(services);
        DisposeDebugProjection<DebugSessionViewModel>(services);

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
        services.GetService<IFileTreeService>()?.Dispose();
        services.GetService<ITerminalHost>()?.Dispose();
    }

    private static void DisposeDebugProjection<T>(IServiceProvider services)
        where T : class
    {
        if (services.GetService(typeof(T)) is IDisposable disposable)
            disposable.Dispose();
    }
}
