using Zaide.Features.Settings.Contracts;

namespace Zaide.Features.Settings.Presentation;

/// <summary>
/// Creates a transient Settings ViewModel and panel pair for shell overlay ownership.
/// Each call returns fresh instances; the factory does not cache or dispose them.
/// </summary>
public interface ISettingsPanelFactory
{
    (SettingsViewModel ViewModel, SettingsPanelView View) Create(
        ISettingsService settings,
        ISecretStore secrets);
}
