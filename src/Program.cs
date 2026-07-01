using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia.Splat;
using System;
using Zaide.Services;
using Zaide.ViewModels;

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
                    services.AddSingleton<ITerminalService, LinuxTerminalService>();
                    services.AddSingleton<IFileTreeService, FileTreeService>();
                    services.AddSingleton<FileTreeViewModel>();
                    services.AddSingleton<MainWindowViewModel>();
                    services.AddSingleton<EditorTabViewModel>();
                    services.AddSingleton<TerminalViewModel>();
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
