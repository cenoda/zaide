using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Heuristic token estimator used after redaction and before budget enforcement.
/// </summary>
internal static class AgentContextTokenEstimator
{
    public const string ExceededBudgetMarker = "[TRUNCATED:exceeded-budget]";

    public static int Estimate(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.Length == 0)
        {
            return 0;
        }

        return (int)Math.Ceiling(content.Length / 4.0);
    }
}
