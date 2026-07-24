using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Terminal result for one action attempt.
/// </summary>
internal sealed class AgentActionResult
{
    public AgentActionResult(
        AgentActionId actionId,
        AgentActionAttemptId attemptId,
        AgentActionResultKind resultKind,
        AgentActionFailureKind? failureKind,
        string summary,
        bool isTerminal = true)
    {
        if (actionId == default)
        {
            throw new ArgumentException("Action id is required.", nameof(actionId));
        }

        if (attemptId == default)
        {
            throw new ArgumentException("Attempt id is required.", nameof(attemptId));
        }

        if (!Enum.IsDefined(resultKind))
        {
            throw new ArgumentOutOfRangeException(nameof(resultKind), resultKind, "Result kind is invalid.");
        }

        if (failureKind is not null && !Enum.IsDefined(failureKind.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureKind),
                failureKind,
                "Failure kind is invalid.");
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("Result summary is required.", nameof(summary));
        }

        if (!isTerminal)
        {
            throw new ArgumentException("Action results must be terminal in Phase 17 M1.", nameof(isTerminal));
        }

        ActionId = actionId;
        AttemptId = attemptId;
        ResultKind = resultKind;
        FailureKind = failureKind;
        Summary = summary.Trim();
        IsTerminal = true;
    }

    public AgentActionId ActionId { get; }

    public AgentActionAttemptId AttemptId { get; }

    public AgentActionResultKind ResultKind { get; }

    public AgentActionFailureKind? FailureKind { get; }

    public string Summary { get; }

    public bool IsTerminal { get; }
}
