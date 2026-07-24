using System;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// One permission decision bound to a single immutable request fingerprint.
/// </summary>
internal sealed class AgentPermissionDecision
{
    public AgentPermissionDecision(
        AgentPermissionDecisionId decisionId,
        AgentActionRequestFingerprint requestFingerprint,
        AgentActionPermissionClassification classification,
        AgentPermissionDecisionStatus status,
        DateTimeOffset publishedAtUtc,
        DateTimeOffset expiresAtUtc,
        bool isAllow)
    {
        if (decisionId == default)
        {
            throw new ArgumentException("Decision id is required.", nameof(decisionId));
        }

        if (requestFingerprint == default)
        {
            throw new ArgumentException("Request fingerprint is required.", nameof(requestFingerprint));
        }

        if (!Enum.IsDefined(classification))
        {
            throw new ArgumentOutOfRangeException(
                nameof(classification),
                classification,
                "Permission classification is invalid.");
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Decision status is invalid.");
        }

        if (publishedAtUtc == default)
        {
            throw new ArgumentException("Published time is required.", nameof(publishedAtUtc));
        }

        if (expiresAtUtc <= publishedAtUtc)
        {
            throw new ArgumentException(
                "Decision expiry must be after publication.",
                nameof(expiresAtUtc));
        }

        DecisionId = decisionId;
        RequestFingerprint = requestFingerprint;
        Classification = classification;
        Status = status;
        PublishedAtUtc = publishedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        IsAllow = isAllow;
    }

    public AgentPermissionDecisionId DecisionId { get; }

    public AgentActionRequestFingerprint RequestFingerprint { get; }

    public AgentActionPermissionClassification Classification { get; }

    public AgentPermissionDecisionStatus Status { get; }

    public DateTimeOffset PublishedAtUtc { get; }

    public DateTimeOffset ExpiresAtUtc { get; }

    public bool IsAllow { get; }
}
