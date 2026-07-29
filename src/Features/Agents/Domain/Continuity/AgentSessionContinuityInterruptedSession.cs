using System;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Domain.Continuity;

internal sealed class AgentSessionContinuityInterruptedSession
{
    public AgentSessionContinuityInterruptedSession(
        AgentSessionContinuityScope scope,
        AgentSessionContinuityClassification classification,
        AgentSessionContinuityCheckpoint latestCheckpoint,
        bool resumeAdmitted,
        bool terminated,
        DateTimeOffset? resumedAtUtc = null,
        DateTimeOffset? terminatedAtUtc = null)
    {
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        LatestCheckpoint = latestCheckpoint ?? throw new ArgumentNullException(nameof(latestCheckpoint));
        Classification = classification;
        ResumeAdmitted = resumeAdmitted;
        Terminated = terminated;
        ResumedAtUtc = resumedAtUtc;
        TerminatedAtUtc = terminatedAtUtc;
    }

    public AgentSessionContinuityScope Scope { get; }

    public AgentSessionContinuityClassification Classification { get; }

    public AgentSessionContinuityCheckpoint LatestCheckpoint { get; }

    public bool ResumeAdmitted { get; }

    public bool Terminated { get; }

    public DateTimeOffset? ResumedAtUtc { get; }

    public DateTimeOffset? TerminatedAtUtc { get; }
}
