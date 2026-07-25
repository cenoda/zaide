using System;
using System.Threading;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// One permission decision bound to a single immutable request fingerprint.
/// All fields except <see cref="Status"/> are immutable; the status advances
/// only through the atomic <see cref="TryConsume"/> transition
/// (Published → Consumed) so one decision can authorize at most one
/// execution.
/// </summary>
internal sealed class AgentPermissionDecision
{
    private int _status;

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
        _status = (int)status;
        PublishedAtUtc = publishedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        IsAllow = isAllow;
    }

    public AgentPermissionDecisionId DecisionId { get; }

    public AgentActionRequestFingerprint RequestFingerprint { get; }

    public AgentActionPermissionClassification Classification { get; }

    public AgentPermissionDecisionStatus Status =>
        (AgentPermissionDecisionStatus)Volatile.Read(ref _status);

    /// <summary>
    /// Atomically transitions the decision from
    /// <see cref="AgentPermissionDecisionStatus.Published"/> to
    /// <see cref="AgentPermissionDecisionStatus.Consumed"/>. Returns
    /// <c>false</c> when the decision is not currently Published — already
    /// consumed, denied, revoked, or expired — so a decision can never
    /// authorize more than one execution and no terminal status can be
    /// consumed.
    /// </summary>
    public bool TryConsume() =>
        Interlocked.CompareExchange(
            ref _status,
            (int)AgentPermissionDecisionStatus.Consumed,
            (int)AgentPermissionDecisionStatus.Published)
        == (int)AgentPermissionDecisionStatus.Published;

    public DateTimeOffset PublishedAtUtc { get; }

    public DateTimeOffset ExpiresAtUtc { get; }

    public bool IsAllow { get; }
}
