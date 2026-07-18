using Zaide.Features.Settings.Contracts;

namespace Zaide.Features.Settings.Presentation;

/// <summary>
/// Default <see cref="ISettingsPanelFactory"/>: constructs a new
/// <see cref="SettingsViewModel"/> and <see cref="SettingsPanelView"/> per call.
/// </summary>
internal sealed class SettingsPanelFactory : ISettingsPanelFactory
{
    public (SettingsViewModel ViewModel, SettingsPanelView View) Create(
        ISettingsService settings,
        ISecretStore secrets)
    {
        var viewModel = new SettingsViewModel(settings, secrets);
        var panel = new SettingsPanelView(viewModel);
        return (viewModel, panel);
    }
}
