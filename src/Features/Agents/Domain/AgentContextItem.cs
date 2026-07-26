using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// One attributed IDE context item before manifest assembly completes.
/// </summary>
internal sealed class AgentContextItem
{
    public AgentContextItem(
        AgentContextSourceId sourceId,
        string content,
        string scopeDescriptor,
        string fingerprint,
        AgentContextRedactionState redactionState,
        int estimatedTokenCount,
        AgentContextProvenance provenance,
        AgentContextRedactionReason? redactionReason = null)
    {
        if (sourceId == default)
        {
            throw new ArgumentException("Context source id is required.", nameof(sourceId));
        }

        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(scopeDescriptor);
        ArgumentNullException.ThrowIfNull(fingerprint);
        ArgumentNullException.ThrowIfNull(provenance);

        if (string.IsNullOrWhiteSpace(scopeDescriptor))
        {
            throw new ArgumentException("Scope descriptor is required.", nameof(scopeDescriptor));
        }

        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            throw new ArgumentException("Fingerprint is required.", nameof(fingerprint));
        }

        if (!Enum.IsDefined(redactionState))
        {
            throw new ArgumentOutOfRangeException(
                nameof(redactionState),
                redactionState,
                "Redaction state is invalid.");
        }

        if (estimatedTokenCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(estimatedTokenCount),
                estimatedTokenCount,
                "Estimated token count cannot be negative.");
        }

        if (redactionState is AgentContextRedactionState.Partial or AgentContextRedactionState.Full
            && redactionReason is null)
        {
            throw new ArgumentException(
                "Redaction reason is required when content was redacted.",
                nameof(redactionReason));
        }

        if (redactionState == AgentContextRedactionState.ProcessingFailed
            && !string.IsNullOrEmpty(content))
        {
            throw new ArgumentException(
                "Processing-failed context items cannot retain content.",
                nameof(content));
        }

        SourceId = sourceId;
        Content = redactionState == AgentContextRedactionState.ProcessingFailed
            ? string.Empty
            : content;
        ScopeDescriptor = scopeDescriptor;
        Fingerprint = fingerprint;
        RedactionState = redactionState;
        RedactionReason = redactionReason;
        EstimatedTokenCount = estimatedTokenCount;
        Provenance = provenance;
    }

    public AgentContextSourceId SourceId { get; }

    public string Content { get; }

    public string ScopeDescriptor { get; }

    public string Fingerprint { get; }

    public AgentContextRedactionState RedactionState { get; }

    public AgentContextRedactionReason? RedactionReason { get; }

    public int EstimatedTokenCount { get; }

    public AgentContextProvenance Provenance { get; }
}
