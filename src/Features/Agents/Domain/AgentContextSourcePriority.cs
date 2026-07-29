using System;
using System.Collections.Generic;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Locked M0 source priority order for deterministic budget enforcement.
/// Lower numeric values indicate higher inclusion priority.
/// </summary>
internal static class AgentContextSourcePriority
{
    private static readonly IReadOnlyDictionary<AgentContextSourceId, int> PriorityBySource =
        new Dictionary<AgentContextSourceId, int>
        {
            [AgentContextSourceId.BuildTestFailure] = 1,
            [AgentContextSourceId.DebugException] = 1,
            [AgentContextSourceId.ActiveFile] = 2,
            [AgentContextSourceId.LanguageDiagnostics] = 3,
            [AgentContextSourceId.TestResultsSummary] = 4,
            [AgentContextSourceId.WorkflowState] = 5,
            [AgentContextSourceId.OpenFiles] = 6,
            [AgentContextSourceId.SourceControlSummary] = 7,
            [AgentContextSourceId.DebugSessionState] = 8,
            [AgentContextSourceId.EditorCaretSelection] = 9,
            [AgentContextSourceId.BuildDiagnostics] = 10,
            [AgentContextSourceId.ProjectContext] = 10,
            [AgentContextSourceId.DurableMemory] = 11,
        };

    public static int GetPriority(AgentContextSourceId sourceId)
    {
        if (sourceId == default)
        {
            throw new ArgumentException("Context source id is required.", nameof(sourceId));
        }

        if (!PriorityBySource.TryGetValue(sourceId, out var priority))
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceId),
                sourceId,
                "Context source id is not registered in the priority order.");
        }

        return priority;
    }

    public static int Compare(AgentContextSourceId left, AgentContextSourceId right)
    {
        var leftPriority = GetPriority(left);
        var rightPriority = GetPriority(right);

        var priorityComparison = leftPriority.CompareTo(rightPriority);
        if (priorityComparison != 0)
        {
            return priorityComparison;
        }

        return string.CompareOrdinal(left.Value, right.Value);
    }
}
