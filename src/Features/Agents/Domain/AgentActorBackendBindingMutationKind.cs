namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Kind of durable actor/backend binding mutation.
/// </summary>
internal enum AgentActorBackendBindingMutationKind
{
    Bind,
    Update,
    Unbind,
}
