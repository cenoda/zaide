using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Avalonia.Splat;
using System;
using System.Net.Http;
using System.Reactive.Concurrency;
using Zaide.Features.Debugging.Infrastructure.Dap;
using Zaide.Features.Language.Infrastructure.Lsp;
using Zaide.App.Shell;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Infrastructure;
using Zaide.Features.Workspace.Contracts;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Workspace.Infrastructure;
using Zaide.Features.Workspace.Presentation;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Infrastructure;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.ProjectSystem.Infrastructure;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.ProjectSystem.Presentation;
using Zaide.Features.Language.Contracts;
using Zaide.Features.Language.Application;
using Zaide.Features.Debugging.Contracts;
using Zaide.Features.Debugging.Application;
using Zaide.Features.Debugging.Presentation;
using Zaide.Features.SourceControl.Domain;
using Zaide.Features.SourceControl.Contracts;
using Zaide.Features.SourceControl.Application;
using Zaide.Features.SourceControl.Infrastructure;
using Zaide.Features.SourceControl.Presentation;
using Zaide.Features.Terminal.Contracts;
using Zaide.Features.Terminal.Application;
using Zaide.Features.Terminal.Infrastructure;
using Zaide.Features.Terminal.Presentation;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Agents.Presentation;

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
        services.AddSingleton<Workspace>();

        // Phase 8.1.1 M1: immutable settings core
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<ICommandRegistry, CommandRegistry>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<StatusBarViewModel>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IEditorSessionFactory, EditorSessionFactory>();
        services.AddSingleton<ITerminalSessionFactory, TerminalSessionFactory>();
        services.AddSingleton<ITerminalHost, TerminalHost>();
        services.AddSingleton<IAgentPanelHost, AgentPanelHost>();
        services.AddSingleton<IFileTreeService, FileTreeService>();
        services.AddSingleton<IScheduler>(_ => ReactiveUI.Avalonia.AvaloniaScheduler.Instance);
        services.AddSingleton<FileTreeViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<CommandPaletteViewModel>();
        services.AddSingleton<EditorSearchViewModel>();
        services.AddSingleton<TownhallState>();
        services.AddSingleton<TownhallViewModel>();
        services.AddSingleton<EditorTabViewModel>();
        services.AddSingleton<SourceControlViewModel>();
        // Phase 8.1.2 M2: secret boundary (no AgentExecutionOptions singleton)
        services.AddSingleton<ISecretStore>(_ =>
            new FileSecretStore(SettingsPathResolver.GetSecretsPath()));
        services.AddSingleton<IAgentExecutionService, AgentExecutionService>();
        services.AddSingleton<IAgentExecutionCoordinator, AgentExecutionCoordinator>();
        services.AddSingleton<MentionParser>();
        services.AddSingleton<IAgentRouter, AgentRouter>();

        // M1: read-only git repository discovery + status read seam
        services.AddSingleton<IGitRepositoryService, GitRepositoryService>();

        // M3: focused snapshot refresh orchestration seam for Source Control
        services.AddSingleton<ISourceControlSnapshotOrchestrator, SourceControlSnapshotOrchestrator>();

        // M1: file diff service for Source Control diff view
        services.AddSingleton<IFileDiffService, FileDiffService>();
        services.AddSingleton<ISourceControlDiffTabService, SourceControlDiffTabService>();

        // Phase 7.4 M1: git mutation seam for stage/unstage operations
        services.AddSingleton<IGitMutationService, GitMutationService>();

        // Phase 8.3 M3: authoritative project-context discovery + service.
        services.AddSingleton<IProjectFileSystem, FileSystemProjectFileSystem>();
        services.AddSingleton<IProjectDiscovery, ProjectDiscovery>();
        services.AddSingleton<IProjectContextService, ProjectContextService>();

        // Phase 12 M1: UI-independent DAP adapter locator and session lifecycle core.
        services.AddSingleton<IDebugAdapterLocator>(_ =>
            new DebugAdapterLocator(Environment.GetEnvironmentVariable("ZAIDE_NETCOREDBG_PATH")));
        services.AddSingleton<IDebugAdapterSessionFactory, DebugAdapterSessionFactory>();
        services.AddSingleton<DebugSessionTimeoutPolicy>();
        services.AddSingleton<IDebugSessionService, DebugSessionService>();

        // Phase 12 M2: workspace-scoped persistent breakpoint storage.
        services.AddSingleton<IBreakpointService, BreakpointService>();

        // Phase 12 M3a: shared project-operation gate and build-to-debug handoff.
        services.AddSingleton<IProjectOperationGate, ProjectOperationGate>();
        services.AddSingleton<IProjectDebugTargetResolver, ProjectDebugTargetResolver>();
        services.AddSingleton<IProjectDebugLaunchService, ProjectDebugLaunchService>();
        services.AddSingleton<DebugSessionViewModel>();
        services.AddSingleton<DebugStackProjectionViewModel>();
        services.AddSingleton<DebugCurrentLocationViewModel>();
        services.AddSingleton<DebugPanelViewModel>();
        services.AddSingleton<EditorBreakpointViewModel>();

        // Phase 11 M1: UI-independent build/run/test process orchestration core.
        services.AddSingleton<IManagedProcessRunner, ManagedProcessRunner>();
        services.AddSingleton<IProjectWorkflowService, ProjectWorkflowService>();

        // Phase 11 M2: structured output projection and workflow commands.
        services.AddSingleton<IProjectOutputService, ProjectOutputService>();
        services.AddSingleton<ProjectWorkflowViewModel>();

        // Phase 11 M3: parsed build diagnostics (Problems merge projection).
        services.AddSingleton<IBuildDiagnosticsService, BuildDiagnosticsService>();

        // Phase 11 M5: structured test results projection.
        services.AddSingleton<ITestResultsService, TestResultsService>();
        services.AddSingleton<TestResultsViewModel>();

        // Phase 10 M1: C# language session (process + StreamJsonRpc transport).
        services.AddSingleton<ILanguageServerBinaryLocator, LanguageServerBinaryLocator>();
        services.AddSingleton<ILanguageServerSessionFactory, CsharpLsSessionFactory>();
        services.AddSingleton<ILanguageSessionService, LanguageSessionService>();
        services.AddSingleton<ILanguageDocumentBridge, LanguageDocumentBridge>();

        // Phase 10 M3: structured diagnostics + Problems projection.
        services.AddSingleton<ILanguageDiagnosticsService, LanguageDiagnosticsService>();
        services.AddSingleton<ProblemsViewModel>();

        // Phase 10 M4: active-document completion and hover.
        services.AddSingleton<ILanguageCompletionService, LanguageCompletionService>();
        services.AddSingleton<ILanguageHoverService, LanguageHoverService>();

        // Phase 10 M5: Go to Definition + document/workspace symbols.
        services.AddSingleton<ILanguageNavigationService, LanguageNavigationService>();
        services.AddSingleton<ILanguageSymbolService, LanguageSymbolService>();

        // Phase 10 M6: whole-document formatting + Format on Save coordination.
        services.AddSingleton<ILanguageFormattingService, LanguageFormattingService>();
        services.AddSingleton<EditorLanguageInputViewModel>();

        services.AddSingleton(_ =>
        {
            var client = new HttpClient();
            // Default timeout for non-streaming requests
            client.Timeout = TimeSpan.FromSeconds(120);
            return client;
        });
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
