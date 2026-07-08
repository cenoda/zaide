using Avalonia;
using Microsoft.Extensions.DependencyInjection;
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
                    services.AddSingleton<SourceControlState>();
                    services.AddSingleton<SourceControlViewModel>();
                    // M1: Register agent execution service seam
                    services.AddSingleton<AgentExecutionOptions>(_ =>
                    {
                        var options = new AgentExecutionOptions();
                        if (Environment.GetEnvironmentVariable("AGENT_API_URL") is { Length: > 0 } url)
                            options.BaseUrl = url;
                        if (Environment.GetEnvironmentVariable("AGENT_API_KEY") is { Length: > 0 } key)
                            options.ApiKey = key;
                        if (Environment.GetEnvironmentVariable("AGENT_MODEL") is { Length: > 0 } model)
                            options.Model = model;
                        return options;
                    });
                    services.AddSingleton<IAgentExecutionService, AgentExecutionService>();
                    services.AddSingleton<IAgentExecutionCoordinator, AgentExecutionCoordinator>();
                    services.AddSingleton<IAgentRouter, AgentRouter>();
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
