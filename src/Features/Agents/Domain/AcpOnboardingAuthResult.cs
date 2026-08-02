using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Result of an ACP authenticate(methodId) bridge call.
/// </summary>
internal sealed class AcpOnboardingAuthResult
{
    private AcpOnboardingAuthResult(bool isSuccess, ActorId actorId, string? message, string? methodId)
    {
        IsSuccess = isSuccess;
        ActorId = actorId;
        Message = message;
        MethodId = methodId;
    }

    public bool IsSuccess { get; }

    public ActorId ActorId { get; }

    public string? Message { get; }

    public string? MethodId { get; }

    public static AcpOnboardingAuthResult Succeeded(ActorId actorId, string methodId) =>
        new(isSuccess: true, actorId, message: null, methodId);

    public static AcpOnboardingAuthResult Failed(ActorId actorId, string message, string? methodId = null) =>
        new(isSuccess: false, actorId, message, methodId);
}
