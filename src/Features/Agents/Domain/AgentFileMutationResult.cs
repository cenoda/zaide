using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Structured, attributable result of one bounded workspace file mutation.
/// Revision and byte length are present only after a confirmed successful write.
/// </summary>
internal sealed class AgentFileMutationResult
{
    private AgentFileMutationResult(
        AgentFileMutationOutcome outcome,
        AgentContentRevision revision,
        long byteLength,
        string summary)
    {
        Outcome = outcome;
        Revision = revision;
        ByteLength = byteLength;
        Summary = summary;
    }

    public AgentFileMutationOutcome Outcome { get; }

    public bool IsSuccess => Outcome == AgentFileMutationOutcome.Succeeded;

    /// <summary>Lowercase SHA-256 digest over the exact bytes on disk after success.</summary>
    public AgentContentRevision Revision { get; }

    /// <summary>Exact number of bytes written or remaining after success; otherwise zero.</summary>
    public long ByteLength { get; }

    public string Summary { get; }

    public static AgentFileMutationResult Success(
        AgentContentRevision revision,
        long byteLength,
        string summary)
    {
        if (revision == default)
        {
            throw new ArgumentException("Content revision is required.", nameof(revision));
        }

        if (byteLength < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(byteLength),
                byteLength,
                "Byte length cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("Success summary is required.", nameof(summary));
        }

        return new AgentFileMutationResult(
            AgentFileMutationOutcome.Succeeded,
            revision,
            byteLength,
            summary.Trim());
    }

    public static AgentFileMutationResult DeleteSuccess(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("Success summary is required.", nameof(summary));
        }

        return new AgentFileMutationResult(
            AgentFileMutationOutcome.Succeeded,
            revision: default,
            byteLength: 0,
            summary.Trim());
    }

    public static AgentFileMutationResult Rejected(AgentFileMutationOutcome outcome, string summary)
    {
        if (outcome == AgentFileMutationOutcome.Succeeded)
        {
            throw new ArgumentException(
                "Use Success for a confirmed mutation.",
                nameof(outcome));
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("Rejection summary is required.", nameof(summary));
        }

        return new AgentFileMutationResult(
            outcome,
            revision: default,
            byteLength: 0,
            summary.Trim());
    }
}
