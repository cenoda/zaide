using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.Avalonia.Splat;
using System;
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
                    services.AddSingleton<IFileTreeService, FileTreeService>();
                    services.AddSingleton<IScheduler>(_ => ReactiveUI.Avalonia.AvaloniaScheduler.Instance);
                    services.AddSingleton<FileTreeViewModel>();
                    services.AddSingleton<MainWindowViewModel>();
                    services.AddSingleton<TownhallState>();
                    services.AddSingleton<TownhallViewModel>();
                    services.AddSingleton<EditorTabViewModel>();
                    services.AddSingleton<SourceControlState>();
                    services.AddSingleton<SourceControlViewModel>();
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
