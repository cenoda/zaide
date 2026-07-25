using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Structured outcome of one post-mutation document reconciliation attempt.
/// </summary>
internal sealed class AgentDocumentReconciliationResult
{
    private AgentDocumentReconciliationResult(
        AgentDocumentReconciliationOutcome outcome,
        string summary)
    {
        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Outcome is invalid.");
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("Summary is required.", nameof(summary));
        }

        Outcome = outcome;
        Summary = summary.Trim();
    }

    public AgentDocumentReconciliationOutcome Outcome { get; }

    public string Summary { get; }

    public static AgentDocumentReconciliationResult Create(
        AgentDocumentReconciliationOutcome outcome,
        string summary) =>
        new(outcome, summary);
}
