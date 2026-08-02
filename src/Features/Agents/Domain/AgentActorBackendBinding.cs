using System;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Explicit per-actor backend and optional ACP runtime binding.
/// </summary>
internal sealed class AgentActorBackendBinding
{
    public AgentActorBackendBinding(
        ActorId actorId,
        AgentBackendId backendId,
        AcpRuntimeIdentity? acpRuntime = null,
        string? expectedAgentName = null,
        string? expectedAgentVersion = null,
        string? selectedAuthMethodId = null,
        AgentAuthenticationConnectionState authenticationState =
            AgentAuthenticationConnectionState.NotRequired,
        long revision = 1)
    {
        if (actorId == default)
        {
            throw new ArgumentException("Actor id is required.", nameof(actorId));
        }

        if (backendId == default)
        {
            throw new ArgumentException("Backend id is required.", nameof(backendId));
        }

        if (revision < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(revision),
                revision,
                "Binding revision must be >= 1.");
        }

        if (backendId == AgentBackendIds.Acp)
        {
            if (acpRuntime is null)
            {
                throw new ArgumentException("ACP runtime identity is required for ACP bindings.", nameof(acpRuntime));
            }

            if (string.IsNullOrWhiteSpace(expectedAgentName))
            {
                throw new ArgumentException("Expected agent name is required for ACP bindings.", nameof(expectedAgentName));
            }

            if (string.IsNullOrWhiteSpace(expectedAgentVersion))
            {
                throw new ArgumentException(
                    "Expected agent version is required for ACP bindings.",
                    nameof(expectedAgentVersion));
            }
        }
        else if (acpRuntime is not null)
        {
            throw new ArgumentException("ACP runtime identity is only valid for ACP bindings.", nameof(acpRuntime));
        }

        ActorId = actorId;
        BackendId = backendId;
        AcpRuntime = acpRuntime;
        ExpectedAgentName = expectedAgentName?.Trim();
        ExpectedAgentVersion = expectedAgentVersion?.Trim();
        SelectedAuthMethodId = NormalizeOptional(selectedAuthMethodId);
        AuthenticationState = authenticationState;
        Revision = revision;
    }

    public ActorId ActorId { get; }

    public AgentBackendId BackendId { get; }

    public AcpRuntimeIdentity? AcpRuntime { get; }

    public string? ExpectedAgentName { get; }

    public string? ExpectedAgentVersion { get; }

    public string? SelectedAuthMethodId { get; }

    public AgentAuthenticationConnectionState AuthenticationState { get; }

    /// <summary>
    /// Durable binding revision. Advanced only by successful bind/update mutations.
    /// </summary>
    public long Revision { get; }

    public AgentActorBackendBinding WithAuthentication(
        string? selectedAuthMethodId,
        AgentAuthenticationConnectionState authenticationState) =>
        new(
            ActorId,
            BackendId,
            AcpRuntime,
            ExpectedAgentName,
            ExpectedAgentVersion,
            selectedAuthMethodId,
            authenticationState,
            Revision);

    public AgentActorBackendBinding WithRevision(long revision) =>
        new(
            ActorId,
            BackendId,
            AcpRuntime,
            ExpectedAgentName,
            ExpectedAgentVersion,
            SelectedAuthMethodId,
            AuthenticationState,
            revision);

    public AgentActorBackendBinding WithClearedRuntimeAuth() =>
        new(
            ActorId,
            BackendId,
            AcpRuntime,
            ExpectedAgentName,
            ExpectedAgentVersion,
            selectedAuthMethodId: null,
            authenticationState: BackendId == AgentBackendIds.Acp
                ? AgentAuthenticationConnectionState.Disconnected
                : AgentAuthenticationConnectionState.NotRequired,
            Revision);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
