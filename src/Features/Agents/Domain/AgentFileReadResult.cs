using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Structured, attributable result of one bounded workspace file read. Content
/// and revision are present only on a successful read; every rejection carries a
/// bounded, non-sensitive summary and never partial content.
/// </summary>
internal sealed class AgentFileReadResult
{
    private AgentFileReadResult(
        AgentFileReadOutcome outcome,
        string? content,
        AgentContentRevision revision,
        long byteLength,
        string summary)
    {
        Outcome = outcome;
        Content = content;
        Revision = revision;
        ByteLength = byteLength;
        Summary = summary;
    }

    public AgentFileReadOutcome Outcome { get; }

    public bool IsSuccess => Outcome == AgentFileReadOutcome.Succeeded;

    /// <summary>Decoded UTF-8 content; non-null only on success.</summary>
    public string? Content { get; }

    /// <summary>Lowercase SHA-256 digest over the exact bytes read on success.</summary>
    public AgentContentRevision Revision { get; }

    /// <summary>Exact number of bytes read on success; otherwise zero.</summary>
    public long ByteLength { get; }

    public string Summary { get; }

    public static AgentFileReadResult Success(
        string content,
        AgentContentRevision revision,
        long byteLength)
    {
        ArgumentNullException.ThrowIfNull(content);
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

        return new AgentFileReadResult(
            AgentFileReadOutcome.Succeeded,
            content,
            revision,
            byteLength,
            $"Read {byteLength} byte(s); revision {revision.Value}.");
    }

    public static AgentFileReadResult Rejected(AgentFileReadOutcome outcome, string summary)
    {
        if (outcome == AgentFileReadOutcome.Succeeded)
        {
            throw new ArgumentException(
                "Use Success for a successful read.",
                nameof(outcome));
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("Rejection summary is required.", nameof(summary));
        }

        return new AgentFileReadResult(
            outcome,
            content: null,
            revision: default,
            byteLength: 0,
            summary: summary.Trim());
    }
}
