using System.Threading.Tasks;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Domain;

namespace Zaide.Tests.Features.Agents.Transparency.Integration;

internal static class Phase23SettingsTestSupport
{
    public static Task EnableTraceCaptureAsync(ISettingsService settings) =>
        SetTraceCaptureAsync(settings, enabled: true);

    public static Task EnableUsageCaptureAsync(ISettingsService settings) =>
        SetUsageCaptureAsync(settings, enabled: true);

    public static Task DisableTraceCaptureAsync(ISettingsService settings) =>
        SetTraceCaptureAsync(settings, enabled: false);

    public static Task DisableUsageCaptureAsync(ISettingsService settings) =>
        SetUsageCaptureAsync(settings, enabled: false);

    public static Task SetTraceCaptureAsync(ISettingsService settings, bool enabled) =>
        settings.UpdateAsync(current => current with
        {
            Agents = current.Agents with { TraceCaptureEnabled = enabled },
        });

    public static Task SetUsageCaptureAsync(ISettingsService settings, bool enabled) =>
        settings.UpdateAsync(current => current with
        {
            Agents = current.Agents with { UsageCaptureEnabled = enabled },
        });

    public static Task<SettingsMutationResult> ApplyAgentsAsync(
        ISettingsService settings,
        AgentsSettings agents) =>
        settings.UpdateAsync(current => current with { Agents = agents });
}
