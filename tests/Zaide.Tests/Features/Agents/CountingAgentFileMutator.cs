using System.Threading;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Tests.Features.Agents;

/// <summary>
/// Test double that records mutation invocations and can return scripted results.
/// </summary>
internal sealed class CountingAgentFileMutator : IAgentFileMutator
{
    private AgentFileMutationResult _result = AgentFileMutationResult.Success(
        AgentContentRevision.FromUtf8Text("mutated"),
        byteLength: 7,
        "Mutation succeeded.");

    private int _applyCount;

    public CountingAgentFileMutator()
    {
    }

    public CountingAgentFileMutator(AgentFileMutationResult result)
    {
        _result = result;
    }

    public int ApplyCount => _applyCount;

    public WorkspaceActionScope? LastScope { get; private set; }

    public AgentFileActionProposal? LastProposal { get; private set; }

    public AgentActionPayload? LastPayload { get; private set; }

    public void SetResult(AgentFileMutationResult result) => _result = result;

    public AgentFileMutationResult Apply(
        WorkspaceActionScope scope,
        AgentFileActionProposal proposal,
        AgentActionPayload payload,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        Interlocked.Increment(ref _applyCount);
        LastScope = scope;
        LastProposal = proposal;
        LastPayload = payload;
        return _result;
    }
}
