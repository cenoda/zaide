using System;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Truthful result of one durable actor/backend binding mutation.
/// </summary>
internal sealed class AgentActorBackendBindingMutationResult
{
    private AgentActorBackendBindingMutationResult(
        AgentActorBackendBindingMutationStatus status,
        AgentActorBackendBindingMutationKind kind,
        ActorId actorId,
        long revision,
        string? message,
        AgentActorBackendBinding? binding)
    {
        Status = status;
        Kind = kind;
        ActorId = actorId;
        Revision = revision;
        Message = message;
        Binding = binding;
    }

    public AgentActorBackendBindingMutationStatus Status { get; }

    public AgentActorBackendBindingMutationKind Kind { get; }

    public ActorId ActorId { get; }

    /// <summary>
    /// Binding revision after a successful mutation, or the current authoritative
    /// revision when the mutation is rejected. Zero when the actor is unbound.
    /// </summary>
    public long Revision { get; }

    public string? Message { get; }

    public AgentActorBackendBinding? Binding { get; }

    public bool IsSuccess => Status == AgentActorBackendBindingMutationStatus.Succeeded;

    public static AgentActorBackendBindingMutationResult Succeeded(
        AgentActorBackendBindingMutationKind kind,
        ActorId actorId,
        long revision,
        AgentActorBackendBinding? binding = null)
    {
        if (actorId == default)
        {
            throw new ArgumentException("Actor id is required.", nameof(actorId));
        }

        if (revision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revision), revision, "Revision cannot be negative.");
        }

        if (kind != AgentActorBackendBindingMutationKind.Unbind && binding is null)
        {
            throw new ArgumentException("Binding is required for successful bind/update.", nameof(binding));
        }

        return new AgentActorBackendBindingMutationResult(
            AgentActorBackendBindingMutationStatus.Succeeded,
            kind,
            actorId,
            revision,
            message: null,
            binding);
    }

    public static AgentActorBackendBindingMutationResult Conflict(
        AgentActorBackendBindingMutationKind kind,
        ActorId actorId,
        long currentRevision,
        string message)
    {
        return Failure(
            AgentActorBackendBindingMutationStatus.Conflict,
            kind,
            actorId,
            currentRevision,
            message);
    }

    public static AgentActorBackendBindingMutationResult Busy(
        AgentActorBackendBindingMutationKind kind,
        ActorId actorId,
        long currentRevision,
        string message)
    {
        return Failure(
            AgentActorBackendBindingMutationStatus.Busy,
            kind,
            actorId,
            currentRevision,
            message);
    }

    public static AgentActorBackendBindingMutationResult ValidationFailed(
        AgentActorBackendBindingMutationKind kind,
        ActorId actorId,
        long currentRevision,
        string message)
    {
        return Failure(
            AgentActorBackendBindingMutationStatus.ValidationFailed,
            kind,
            actorId,
            currentRevision,
            message);
    }

    public static AgentActorBackendBindingMutationResult PersistenceFailed(
        AgentActorBackendBindingMutationKind kind,
        ActorId actorId,
        long currentRevision,
        string message)
    {
        return Failure(
            AgentActorBackendBindingMutationStatus.PersistenceFailed,
            kind,
            actorId,
            currentRevision,
            message);
    }

    public static AgentActorBackendBindingMutationResult RecoveryRequired(
        ActorId actorId,
        string message)
    {
        return Failure(
            AgentActorBackendBindingMutationStatus.RecoveryRequired,
            AgentActorBackendBindingMutationKind.Bind,
            actorId,
            currentRevision: 0,
            message);
    }

    private static AgentActorBackendBindingMutationResult Failure(
        AgentActorBackendBindingMutationStatus status,
        AgentActorBackendBindingMutationKind kind,
        ActorId actorId,
        long currentRevision,
        string message)
    {
        if (status == AgentActorBackendBindingMutationStatus.Succeeded)
        {
            throw new ArgumentException("Use Succeeded for a successful mutation.", nameof(status));
        }

        if (actorId == default)
        {
            throw new ArgumentException("Actor id is required.", nameof(actorId));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Failure message is required.", nameof(message));
        }

        return new AgentActorBackendBindingMutationResult(
            status,
            kind,
            actorId,
            currentRevision,
            message.Trim(),
            binding: null);
    }
}
