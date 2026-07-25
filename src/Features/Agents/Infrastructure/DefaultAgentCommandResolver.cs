using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Infrastructure;

/// <summary>
/// Fail-closed production resolver for Phase 17 M1.
/// </summary>
/// <remarks>
/// Command resolution requires a trusted filesystem-aware implementation that
/// can walk PATH, resolve symlinks to their canonical targets, and verify
/// executable file attributes. That resolver does not yet exist in Phase 17 M1,
/// so this implementation rejects every command payload.
///
/// Contract tests that need to exercise fingerprint, display, and denylist
/// binding use a test-only fake resolver that accepts absolute paths in
/// controlled test scenarios.
/// </remarks>
internal sealed class DefaultAgentCommandResolver : IAgentCommandResolver
{
    public bool TryResolve(
        AgentExecuteCommandActionPayload payload,
        out AgentResolvedCommand? resolvedCommand,
        out string? error)
    {
        resolvedCommand = null;
        error = "Command resolution requires a trusted filesystem-aware "
                + "infrastructure resolver that is not available in "
                + "Phase 17 M1. No command action is permission-ready.";
        return false;
    }
}
