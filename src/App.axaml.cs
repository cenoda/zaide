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
            desktop.MainWindow = new MainWindow(settings, secrets, registry, statusBar) { ViewModel = vm };

            // Dispose the terminal host on exit so the active session's shell
            // process is killed and doesn't outlive the app.
            desktop.Exit += (_, _) =>
            {
                // Phase 8.3 M3: explicit shutdown of the project-context service
                // so its WorkspaceFolderChanged subscription is released and any
                // in-flight work is invalidated. App does not rely on implicit
                // root-provider disposal.
                Services.GetRequiredService<IProjectContextService>().Dispose();
                Services.GetService<ITerminalHost>()?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
