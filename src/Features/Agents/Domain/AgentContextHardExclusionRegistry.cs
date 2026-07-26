using System.Linq;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Hard exclusion registry. Phase 18 provides no escape hatch for these categories.
/// </summary>
internal static class AgentContextHardExclusionRegistry
{
    public static bool IsUnconditionallyExcluded(AgentContextHardExclusionId exclusionId) =>
        AgentContextHardExclusionId.All.Contains(exclusionId);

    public static string Describe(AgentContextHardExclusionId exclusionId) =>
        exclusionId switch
        {
            var id when id == AgentContextHardExclusionId.TerminalScrollback =>
                "Raw terminal scrollback content is excluded at all policy levels.",
            var id when id == AgentContextHardExclusionId.DebugVariableWatchTrees =>
                "Debug variable and watch trees are excluded at all policy levels.",
            var id when id == AgentContextHardExclusionId.EnvironmentProcessSecrets =>
                "Environment variables and process environment are excluded at all policy levels.",
            var id when id == AgentContextHardExclusionId.FullLspInternals =>
                "Full LSP protocol internals are excluded at all policy levels.",
            var id when id == AgentContextHardExclusionId.BinaryFileContent =>
                "Binary file content is excluded at all policy levels.",
            var id when id == AgentContextHardExclusionId.RedactionPatternMatch =>
                "Content matching redaction patterns is excluded at all policy levels.",
            _ => "Unknown hard exclusion category.",
        };
}
