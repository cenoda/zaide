using System;
using System.Collections.Generic;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Result of an ACP configuration probe (launch + initialize + identity verify).
/// </summary>
internal sealed class AcpOnboardingProbeResult
{
    private AcpOnboardingProbeResult(
        bool isSuccess,
        ActorId actorId,
        string? message,
        IReadOnlyList<string> authMethodIds,
        bool logoutSupported,
        string? observedAgentName,
        string? observedAgentVersion)
    {
        IsSuccess = isSuccess;
        ActorId = actorId;
        Message = message;
        AuthMethodIds = authMethodIds ?? throw new ArgumentNullException(nameof(authMethodIds));
        LogoutSupported = logoutSupported;
        ObservedAgentName = observedAgentName;
        ObservedAgentVersion = observedAgentVersion;
    }

    public bool IsSuccess { get; }

    public ActorId ActorId { get; }

    public string? Message { get; }

    public IReadOnlyList<string> AuthMethodIds { get; }

    public bool LogoutSupported { get; }

    public string? ObservedAgentName { get; }

    public string? ObservedAgentVersion { get; }

    public static AcpOnboardingProbeResult Succeeded(
        ActorId actorId,
        IReadOnlyList<string> authMethodIds,
        bool logoutSupported,
        string? observedAgentName,
        string? observedAgentVersion) =>
        new(
            isSuccess: true,
            actorId,
            message: null,
            authMethodIds,
            logoutSupported,
            observedAgentName,
            observedAgentVersion);

    public static AcpOnboardingProbeResult Failed(ActorId actorId, string message) =>
        new(
            isSuccess: false,
            actorId,
            message,
            authMethodIds: Array.Empty<string>(),
            logoutSupported: false,
            observedAgentName: null,
            observedAgentVersion: null);
}
