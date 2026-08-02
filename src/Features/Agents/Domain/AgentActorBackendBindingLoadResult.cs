using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Result of loading the durable binding document at store construction.
/// </summary>
internal sealed class AgentActorBackendBindingLoadResult
{
    public AgentActorBackendBindingLoadResult(
        AgentActorBackendBindingLoadState state,
        string? recoveryError = null)
    {
        State = state;
        RecoveryError = string.IsNullOrWhiteSpace(recoveryError)
            ? null
            : recoveryError.Trim();
    }

    public AgentActorBackendBindingLoadState State { get; }

    public string? RecoveryError { get; }

    public bool HasRecoveryError => !string.IsNullOrEmpty(RecoveryError);

    public static AgentActorBackendBindingLoadResult Empty() =>
        new(AgentActorBackendBindingLoadState.Empty);

    public static AgentActorBackendBindingLoadResult Loaded() =>
        new(AgentActorBackendBindingLoadState.Loaded);

    public static AgentActorBackendBindingLoadResult RecoveredFromLastKnownGood() =>
        new(AgentActorBackendBindingLoadState.RecoveredFromLastKnownGood);

    public static AgentActorBackendBindingLoadResult UnboundWithRecoveryError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Recovery error message is required.", nameof(message));
        }

        return new AgentActorBackendBindingLoadResult(
            AgentActorBackendBindingLoadState.UnboundWithRecoveryError,
            message);
    }

    public static AgentActorBackendBindingLoadResult UnsupportedSchema(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Unsupported-schema message is required.", nameof(message));
        }

        return new AgentActorBackendBindingLoadResult(
            AgentActorBackendBindingLoadState.UnsupportedSchema,
            message);
    }
}
