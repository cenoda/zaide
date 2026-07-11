using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Avalonia.Splat;
using System;
using System.Net.Http;
using System.Reactive.Concurrency;
using Zaide.Services;
using Zaide.ViewModels;
using Zaide.Models;

namespace Zaide;

class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseReactiveUIWithMicrosoftDependencyResolver(
                containerConfig: services =>
                {
                    services.AddSingleton<Models.Workspace>();

                    // Phase 8.1.1 M1: immutable settings core
                    services.AddLogging(builder => builder.AddConsole());
                    services.AddSingleton<ICommandRegistry, CommandRegistry>();
                    services.AddSingleton<ISettingsService, SettingsService>();
                    services.AddSingleton<StatusBarViewModel>();
                    services.AddSingleton<IFileService, FileService>();
                    services.AddSingleton<ITerminalSessionFactory, TerminalSessionFactory>();
                    services.AddSingleton<ITerminalHost, TerminalHost>();
                    services.AddSingleton<IAgentPanelHost, AgentPanelHost>();
                    services.AddSingleton<IFileTreeService, FileTreeService>();
                    services.AddSingleton<IScheduler>(_ => ReactiveUI.Avalonia.AvaloniaScheduler.Instance);
                    services.AddSingleton<FileTreeViewModel>();
                    services.AddSingleton<MainWindowViewModel>();
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

                    // Phase 7.4 M1: git mutation seam for stage/unstage operations
                    services.AddSingleton<IGitMutationService, GitMutationService>();

                    services.AddSingleton(_ =>
                    {
                        var client = new HttpClient();
                        // Default timeout for non-streaming requests
                        client.Timeout = TimeSpan.FromSeconds(120);
                        return client;
                    });

                    services.AddTransient<EditorViewModel>(sp =>
                        new EditorViewModel(new Models.Document(""), sp.GetRequiredService<IFileService>()));
                },
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
