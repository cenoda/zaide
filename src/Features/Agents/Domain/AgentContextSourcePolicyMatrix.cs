using System;
using System.Collections.Generic;
using System.Linq;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Locked source-by-policy matrix and default token budgets for Phase 18.
/// </summary>
internal static class AgentContextSourcePolicyMatrix
{
    private static readonly HashSet<(AgentContextSourceId Source, AgentContextPolicyLevel Level)> Included =
        new()
        {
            (AgentContextSourceId.BuildTestFailure, AgentContextPolicyLevel.Minimal),
            (AgentContextSourceId.DebugException, AgentContextPolicyLevel.Minimal),
            (AgentContextSourceId.ProjectContext, AgentContextPolicyLevel.Minimal),
            (AgentContextSourceId.BuildTestFailure, AgentContextPolicyLevel.Standard),
            (AgentContextSourceId.DebugException, AgentContextPolicyLevel.Standard),
            (AgentContextSourceId.ProjectContext, AgentContextPolicyLevel.Standard),
            (AgentContextSourceId.ActiveFile, AgentContextPolicyLevel.Standard),
            (AgentContextSourceId.LanguageDiagnostics, AgentContextPolicyLevel.Standard),
            (AgentContextSourceId.BuildDiagnostics, AgentContextPolicyLevel.Standard),
            (AgentContextSourceId.TestResultsSummary, AgentContextPolicyLevel.Standard),
            (AgentContextSourceId.WorkflowState, AgentContextPolicyLevel.Standard),
            (AgentContextSourceId.BuildTestFailure, AgentContextPolicyLevel.Detailed),
            (AgentContextSourceId.DebugException, AgentContextPolicyLevel.Detailed),
            (AgentContextSourceId.ProjectContext, AgentContextPolicyLevel.Detailed),
            (AgentContextSourceId.ActiveFile, AgentContextPolicyLevel.Detailed),
            (AgentContextSourceId.LanguageDiagnostics, AgentContextPolicyLevel.Detailed),
            (AgentContextSourceId.BuildDiagnostics, AgentContextPolicyLevel.Detailed),
            (AgentContextSourceId.TestResultsSummary, AgentContextPolicyLevel.Detailed),
            (AgentContextSourceId.WorkflowState, AgentContextPolicyLevel.Detailed),
            (AgentContextSourceId.OpenFiles, AgentContextPolicyLevel.Detailed),
            (AgentContextSourceId.SourceControlSummary, AgentContextPolicyLevel.Detailed),
            (AgentContextSourceId.DebugSessionState, AgentContextPolicyLevel.Detailed),
            (AgentContextSourceId.EditorCaretSelection, AgentContextPolicyLevel.Detailed),
            (AgentContextSourceId.DurableMemory, AgentContextPolicyLevel.Standard),
            (AgentContextSourceId.DurableMemory, AgentContextPolicyLevel.Detailed),
        };

    public static bool DefinesSource(AgentContextSourceId sourceId) =>
        AgentContextSourceId.All.Contains(sourceId);

    public static bool IsSourceIncluded(
        AgentContextSourceId sourceId,
        AgentContextPolicyLevel policyLevel)
    {
        if (sourceId == default)
        {
            throw new ArgumentException("Context source id is required.", nameof(sourceId));
        }

        if (!Enum.IsDefined(policyLevel))
        {
            throw new ArgumentOutOfRangeException(
                nameof(policyLevel),
                policyLevel,
                "Policy level is invalid.");
        }

        if (policyLevel == AgentContextPolicyLevel.Off)
        {
            return false;
        }

        return Included.Contains((sourceId, policyLevel));
    }

    public static int GetDefaultTokenBudget(AgentContextPolicyLevel policyLevel) =>
        policyLevel switch
        {
            AgentContextPolicyLevel.Off => 0,
            AgentContextPolicyLevel.Minimal => 2_000,
            AgentContextPolicyLevel.Standard => 4_000,
            AgentContextPolicyLevel.Detailed => 8_000,
            _ => throw new ArgumentOutOfRangeException(
                nameof(policyLevel),
                policyLevel,
                "Policy level is invalid."),
        };
}
