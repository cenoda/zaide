namespace Zaide.Features.Agents.Application;

/// <summary>
/// Truthful Native Harness provider configuration check used by binding
/// projection. Lives outside presentation so adversarial presentation scans
/// never require secret-field identifiers in UI sources.
/// </summary>
internal static class NativeHarnessProviderConfigured
{
    public static bool IsConfigured(AgentExecutionOptions options)
    {
        if (options is null)
        {
            return false;
        }

        // Matches NativeHarnessAgentBackend.IsConfigured: base URL, model, and
        // key material non-empty. Never returns the key value.
        return !string.IsNullOrWhiteSpace(options.BaseUrl)
            && !string.IsNullOrWhiteSpace(options.Model)
            && !string.IsNullOrWhiteSpace(options.ApiKey);
    }
}
