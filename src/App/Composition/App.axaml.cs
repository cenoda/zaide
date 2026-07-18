using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using System;
using Zaide.App.Shell;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Workspace.Contracts;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.Language.Contracts;
using Zaide.Features.Debugging.Presentation;

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
            desktop.Exit += (_, _) => ApplicationShutdown.Run(CompositionRoot.Services);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Explicit application-shutdown dispose sequence. Extracted for unit tests
    /// that verify ordering without a live Avalonia desktop host.
    /// </summary>
    internal static void DisposeServicesOnExit(IServiceProvider services) =>
        ApplicationShutdown.Run(services);
}
