namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Terminal result categories for one action attempt.
/// </summary>
internal enum AgentActionResultKind
{
    Succeeded,
    Failed,
    Denied,
    Revoked,
    Conflict,
    Cancelled,
    Indeterminate,
    DuplicateReplay,
}

/// <summary>
/// Failure causes represented by terminal action results.
/// </summary>
internal enum AgentActionFailureKind
{
    InvalidRequest,
    PolicyDenied,
    PermissionDenied,
    PermissionExpired,
    PermissionUnavailable,
    BudgetExceeded,
    StaleWorkspace,
    StaleBaseRevision,
    PathRejected,
    ConcurrentActionRejected,
    CorrelationKeyMismatch,
    BrokerRevoked,
    BrokerUnavailable,
    ExecutionFailed,
    Indeterminate,
}
