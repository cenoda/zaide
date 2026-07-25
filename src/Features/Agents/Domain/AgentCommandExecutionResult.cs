using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Structured, bounded result of one Phase 17 command execution.
/// </summary>
internal sealed class AgentCommandExecutionResult
{
    private AgentCommandExecutionResult(
        AgentCommandExecutionOutcome outcome,
        int? exitCode,
        AgentCommandStreamCapture standardOutput,
        AgentCommandStreamCapture standardError,
        string summary)
    {
        Outcome = outcome;
        ExitCode = exitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
        Summary = summary;
    }

    public AgentCommandExecutionOutcome Outcome { get; }

    public bool IsSuccess => Outcome == AgentCommandExecutionOutcome.Succeeded;

    public int? ExitCode { get; }

    public AgentCommandStreamCapture StandardOutput { get; }

    public AgentCommandStreamCapture StandardError { get; }

    public string Summary { get; }

    public static AgentCommandExecutionResult Success(
        int exitCode,
        AgentCommandStreamCapture standardOutput,
        AgentCommandStreamCapture standardError,
        string summary)
    {
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);
        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("Success summary is required.", nameof(summary));
        }

        return new AgentCommandExecutionResult(
            AgentCommandExecutionOutcome.Succeeded,
            exitCode,
            standardOutput,
            standardError,
            summary.Trim());
    }

    public static AgentCommandExecutionResult Terminal(
        AgentCommandExecutionOutcome outcome,
        int? exitCode,
        AgentCommandStreamCapture standardOutput,
        AgentCommandStreamCapture standardError,
        string summary)
    {
        if (outcome == AgentCommandExecutionOutcome.Succeeded)
        {
            throw new ArgumentException("Use Success for a zero-exit completion.", nameof(outcome));
        }

        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);
        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("Terminal summary is required.", nameof(summary));
        }

        return new AgentCommandExecutionResult(
            outcome,
            exitCode,
            standardOutput,
            standardError,
            summary.Trim());
    }
}
