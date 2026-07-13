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
            desktop.Exit += (_, _) =>
            {
                // Phase 11 M1: cancel and kill workflow dotnet trees before language
                // session teardown so child processes are never orphaned.
                Services.GetRequiredService<IProjectWorkflowService>().Dispose();

                // Phase 8.3 M3: explicit shutdown of the project-context service
                // so its WorkspaceFolderChanged subscription is released and any
                // in-flight work is invalidated. App does not rely on implicit
                // root-provider disposal.
                // Tear down language features before document sync/session teardown.
                Services.GetRequiredService<ILanguageFormattingService>().Dispose();
                Services.GetRequiredService<ILanguageNavigationService>().Dispose();
                Services.GetRequiredService<ILanguageSymbolService>().Dispose();
                Services.GetRequiredService<ILanguageCompletionService>().Dispose();
                Services.GetRequiredService<ILanguageHoverService>().Dispose();
                Services.GetRequiredService<ILanguageDiagnosticsService>().Dispose();
                Services.GetRequiredService<ILanguageDocumentBridge>().Dispose();
                Services.GetRequiredService<ILanguageSessionService>().Dispose();
                Services.GetRequiredService<IProjectContextService>().Dispose();
                Services.GetService<ITerminalHost>()?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
