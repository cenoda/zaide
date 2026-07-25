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
        bool isTerminal = true,
        string? content = null,
        AgentContentRevision revision = default,
        long byteLength = 0,
        AgentCommandExecutionResult? commandExecution = null)
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

        if (content is not null && byteLength < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(byteLength),
                byteLength,
                "Byte length cannot be negative.");
        }

        ActionId = actionId;
        AttemptId = attemptId;
        ResultKind = resultKind;
        FailureKind = failureKind;
        Summary = summary.Trim();
        IsTerminal = true;
        Content = content;
        Revision = revision;
        ByteLength = byteLength;
        CommandExecution = commandExecution;
    }

    public AgentActionId ActionId { get; }

    public AgentActionAttemptId AttemptId { get; }

    public AgentActionResultKind ResultKind { get; }

    public AgentActionFailureKind? FailureKind { get; }

    public string Summary { get; }

    public bool IsTerminal { get; }

    /// <summary>
    /// Decoded UTF-8 file content; non-null only on a successful read result.
    /// </summary>
    public string? Content { get; }

    /// <summary>
    /// Lowercase SHA-256 digest over the exact bytes; non-default only on a
    /// successful read result.
    /// </summary>
    public AgentContentRevision Revision { get; }

    /// <summary>
    /// Exact number of bytes read; non-zero only on a successful read result.
    /// </summary>
    public long ByteLength { get; }

    /// <summary>
    /// Bounded command execution evidence; non-null only on command results.
    /// </summary>
    public AgentCommandExecutionResult? CommandExecution { get; }
}
