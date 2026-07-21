using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Backend observation taxonomy for the Phase 15 compatibility path.
/// </summary>
internal enum AgentBackendEventKind
{
    MessageCompleted,
    FailureObserved,
}

/// <summary>
/// Base type for one concrete backend observation payload.
/// </summary>
internal abstract class AgentBackendEventPayload
{
}

/// <summary>
/// Non-streaming assistant completion reported by a backend.
/// </summary>
internal sealed class AgentBackendMessageCompletedPayload : AgentBackendEventPayload
{
    public AgentBackendMessageCompletedPayload(string assistantText)
    {
        if (string.IsNullOrWhiteSpace(assistantText))
        {
            throw new ArgumentException("Assistant text is required.", nameof(assistantText));
        }

        AssistantText = assistantText;
    }

    public string AssistantText { get; }
}

/// <summary>
/// Failure observation reported by a backend.
/// </summary>
internal sealed class AgentBackendFailurePayload : AgentBackendEventPayload
{
    public AgentBackendFailurePayload(AgentFailureKind failureKind, string reason)
    {
        if (!Enum.IsDefined(failureKind))
        {
            throw new ArgumentOutOfRangeException(nameof(failureKind), failureKind, "Failure kind is invalid.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Failure reason is required.", nameof(reason));
        }

        FailureKind = failureKind;
        Reason = reason;
    }

    public AgentFailureKind FailureKind { get; }

    public string Reason { get; }
}

/// <summary>
/// Immutable backend observation emitted during one admitted run attempt.
/// </summary>
internal sealed class AgentBackendEvent
{
    public AgentBackendEvent(
        AgentBackendEventKind kind,
        DateTimeOffset occurredAtUtc,
        AgentBackendEventPayload payload)
    {
        if (occurredAtUtc == default)
        {
            throw new ArgumentException("Occurred time is required.", nameof(occurredAtUtc));
        }

        ArgumentNullException.ThrowIfNull(payload);
        if (!PayloadMatchesKind(kind, payload))
        {
            throw new ArgumentException(
                "Backend event kind and payload type do not match.",
                nameof(payload));
        }

        Kind = kind;
        OccurredAtUtc = occurredAtUtc;
        Payload = payload;
    }

    public AgentBackendEventKind Kind { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public AgentBackendEventPayload Payload { get; }

    private static bool PayloadMatchesKind(AgentBackendEventKind kind, AgentBackendEventPayload payload) =>
        kind switch
        {
            AgentBackendEventKind.MessageCompleted => payload is AgentBackendMessageCompletedPayload,
            AgentBackendEventKind.FailureObserved => payload is AgentBackendFailurePayload,
            _ => false,
        };
}
