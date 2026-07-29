using System;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Presentation;

/// <summary>
/// Reactive presenter for per-actor backend and authentication binding state.
/// </summary>
internal sealed class AgentBackendBindingPresenter
{
    private readonly IAgentActorBackendSelectionService _selectionService;

    public AgentBackendBindingPresenter(IAgentActorBackendSelectionService selectionService)
    {
        _selectionService = selectionService
            ?? throw new ArgumentNullException(nameof(selectionService));
    }

    public event EventHandler<ActorId>? BindingChanged;

    public AgentActorBackendBindingSnapshot GetSnapshot(ActorId actorId) =>
        _selectionService.GetSnapshot(actorId);

    public void BindNativeHarness(ActorId actorId)
    {
        _selectionService.BindNativeHarness(actorId);
        BindingChanged?.Invoke(this, actorId);
    }

    public void BindAcpRuntime(
        ActorId actorId,
        AcpRuntimeIdentity runtimeIdentity,
        string expectedAgentName,
        string expectedAgentVersion)
    {
        _selectionService.BindAcpRuntime(
            actorId,
            runtimeIdentity,
            expectedAgentName,
            expectedAgentVersion);
        BindingChanged?.Invoke(this, actorId);
    }
}
