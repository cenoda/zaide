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
                    services.AddSingleton<IFileService, FileService>();
                    services.AddSingleton<FileTreeService>();
                    services.AddSingleton<FileTreeViewModel>();
                    services.AddSingleton<MainWindowViewModel>();
                    services.AddSingleton<EditorTabViewModel>();
                    services.AddTransient<EditorViewModel>();
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
