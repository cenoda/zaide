using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Result of a capability-gated ACP logout.
/// </summary>
internal sealed class AcpOnboardingLogoutResult
{
    private AcpOnboardingLogoutResult(bool isSuccess, ActorId actorId, string? message)
    {
        IsSuccess = isSuccess;
        ActorId = actorId;
        Message = message;
    }

    public bool IsSuccess { get; }

    public ActorId ActorId { get; }

    public string? Message { get; }

    public static AcpOnboardingLogoutResult Succeeded(ActorId actorId) =>
        new(isSuccess: true, actorId, message: null);

    public static AcpOnboardingLogoutResult Failed(ActorId actorId, string message) =>
        new(isSuccess: false, actorId, message);
}
