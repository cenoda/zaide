using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using System;
using Zaide.App.Composition;
using Zaide.App.Shell;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Presentation;
using Zaide.Features.Workspace.Contracts;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.Language.Contracts;
using Zaide.Features.Debugging.Presentation;
using Zaide.Features.Agents.Application.Continuity;
using Zaide.Features.Agents.Application.Transparency;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Agents.Presentation.Transparency;
using Zaide.UI.DesignSystem;

namespace Zaide.App.Composition;
public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ControlThemeCatalog.Register(this);
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
            var settingsPanelFactory =
                CompositionRoot.Services.GetRequiredService<ISettingsPanelFactory>();

            // Phase 9 M1: eagerly resolve the palette VM so it registers
            // palette.open in the ICommandRegistry singleton before
            // MainWindow.MaterializeRegistryBindings() materialises Ctrl+Shift+P.
            var paletteVm = CompositionRoot.Services.GetRequiredService<CommandPaletteViewModel>();
            var searchVm = CompositionRoot.Services.GetRequiredService<EditorSearchViewModel>();
            var editorUiDispatcher = CompositionRoot.Services.GetRequiredService<IEditorUiDispatcher>();
            var languageInputVm = CompositionRoot.Services.GetRequiredService<EditorLanguageInputViewModel>();
            var transparencyManagement = CompositionRoot.Services
                .GetRequiredService<AgentTransparencyManagementViewModel>();
            AgentTransparencyCommandRegistration.Register(registry, transparencyManagement);

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
                editorUiDispatcher,
                languageInputVm,
                editorBreakpointVm,
                debugCurrentLocationVm,
                settingsPanelFactory)
            {
                ViewModel = vm,
            };

            // Phase 17 M3: attach the permission review surface to the owned
            // main window so user review (Allow/Deny) is reachable in
            // production. Without an owner the presenter fails closed and
            // requests are rejected as PermissionUnavailable.
            var permissionPresenter = CompositionRoot.Services
                .GetRequiredService<IAgentPermissionDialogPresenter>();
            if (permissionPresenter is PermissionReviewDialogPresenter reviewDialogPresenter)
            {
                reviewDialogPresenter.SetOwner(desktop.MainWindow);
            }

            // Phase 21 M4: reconcile interrupted sessions on startup without
            // resuming side-effecting work. Construct the event subscriber so
            // lifecycle checkpoints are recorded for the app lifetime.
            CompositionRoot.Services
                .GetRequiredService<AgentSessionContinuityStartupReconciler>()
                .ReconcileOnStartupIfNeeded();
            _ = CompositionRoot.Services.GetRequiredService<AgentSessionContinuityEventSubscriber>();
            _ = CompositionRoot.Services.GetRequiredService<AgentTransparencySettingsSync>();
            CompositionRoot.Services
                .GetRequiredService<AgentSessionContinuityWorkspaceOpenReconciler>()
                .ReconcileOnWorkspaceOpenIfNeeded();

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
