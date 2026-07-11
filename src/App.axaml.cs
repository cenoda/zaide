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
            var statusBar = Services.GetRequiredService<StatusBarViewModel>();
            desktop.MainWindow = new MainWindow(settings, secrets, statusBar) { ViewModel = vm };

            // Dispose the terminal host on exit so the active session's shell
            // process is killed and doesn't outlive the app.
            desktop.Exit += (_, _) =>
            {
                Services.GetService<ITerminalHost>()?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
