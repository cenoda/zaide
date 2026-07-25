namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Denylist classification for a resolved command executable.
/// </summary>
internal enum AgentCommandDenylistClassification
{
    Allowed,
    DeniedShellInterpreter,
    DeniedPrivilegeEscalation,
}

/// <summary>
/// Immutable denylist outcome bound to one canonical absolute executable path.
/// </summary>
internal sealed class AgentCommandDenylistResult
{
    private AgentCommandDenylistResult(
        AgentCommandDenylistClassification classification,
        string canonicalAbsoluteExecutablePath)
    {
        Classification = classification;
        CanonicalAbsoluteExecutablePath = canonicalAbsoluteExecutablePath;
    }

    public AgentCommandDenylistClassification Classification { get; }

    public string CanonicalAbsoluteExecutablePath { get; }

    public bool IsDenied => Classification != AgentCommandDenylistClassification.Allowed;

    public static AgentCommandDenylistResult Allowed(string canonicalAbsoluteExecutablePath) =>
        new(AgentCommandDenylistClassification.Allowed, canonicalAbsoluteExecutablePath);

    public static AgentCommandDenylistResult Denied(
        AgentCommandDenylistClassification classification,
        string canonicalAbsoluteExecutablePath) =>
        new(classification, canonicalAbsoluteExecutablePath);
}
