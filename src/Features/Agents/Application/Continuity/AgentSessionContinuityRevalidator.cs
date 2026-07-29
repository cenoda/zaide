using System;
using System.Collections.Generic;
using System.Linq;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Contracts.Continuity;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Continuity;

namespace Zaide.Features.Agents.Application.Continuity;

internal sealed class AgentSessionContinuityRevalidator
{
    private readonly IAgentActorBackendBindingStore _bindingStore;
    private readonly IReadOnlyDictionary<AgentBackendId, IAgentBackendContinuityAdapter> _adapters;

    public AgentSessionContinuityRevalidator(
        IAgentActorBackendBindingStore bindingStore,
        IEnumerable<IAgentBackendContinuityAdapter> adapters)
    {
        _bindingStore = bindingStore ?? throw new ArgumentNullException(nameof(bindingStore));
        _adapters = (adapters ?? throw new ArgumentNullException(nameof(adapters)))
            .ToDictionary(adapter => adapter.BackendId);
    }

    public AgentSessionContinuityClassification ClassifyCheckpoint(
        AgentSessionContinuityCheckpoint checkpoint,
        string currentWorkspaceRoot)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);

        if (!string.Equals(
                checkpoint.Scope.WorkspaceRoot,
                currentWorkspaceRoot,
                StringComparison.Ordinal))
        {
            return AgentSessionContinuityClassification.Indeterminate;
        }

        if (!_bindingStore.TryGetBinding(checkpoint.Scope.ActorId, out var binding))
        {
            return AgentSessionContinuityClassification.Indeterminate;
        }

        if (binding.BackendId != checkpoint.Scope.BackendId)
        {
            return AgentSessionContinuityClassification.Indeterminate;
        }

        var fingerprint = AgentSessionContinuityBindingFingerprint.Compute(
            checkpoint.Scope.ActorId,
            checkpoint.Scope.BackendId,
            currentWorkspaceRoot,
            binding.AcpRuntime?.ExecutablePath,
            binding.ExpectedAgentName,
            binding.ExpectedAgentVersion);

        if (!string.Equals(fingerprint, checkpoint.BindingFingerprint, StringComparison.Ordinal))
        {
            return AgentSessionContinuityClassification.Indeterminate;
        }

        if (!_adapters.TryGetValue(checkpoint.Scope.BackendId, out var adapter))
        {
            return AgentSessionContinuityClassification.Indeterminate;
        }

        var capability = adapter.GetCapabilityRow();
        if (!capability.CheckpointSupported)
        {
            return AgentSessionContinuityClassification.Indeterminate;
        }

        if (checkpoint.SessionStatus is AgentSessionStatus.Ended)
        {
            return AgentSessionContinuityClassification.Terminal;
        }

        if (checkpoint.RunStatus is AgentRunStatus.Running or AgentRunStatus.Accepted)
        {
            return AgentSessionContinuityClassification.Recoverable;
        }

        if (checkpoint.RunStatus is AgentRunStatus.Disconnected or AgentRunStatus.Indeterminate)
        {
            return AgentSessionContinuityClassification.Indeterminate;
        }

        if (checkpoint.RunStatus is AgentRunStatus.Completed
            or AgentRunStatus.Failed
            or AgentRunStatus.Cancelled
            or AgentRunStatus.TimedOut)
        {
            return AgentSessionContinuityClassification.Terminal;
        }

        return AgentSessionContinuityClassification.Recoverable;
    }

    public bool CanResume(AgentSessionContinuityCheckpoint checkpoint)
    {
        if (ClassifyCheckpoint(checkpoint, checkpoint.Scope.WorkspaceRoot)
            != AgentSessionContinuityClassification.Recoverable)
        {
            return false;
        }

        if (!_adapters.TryGetValue(checkpoint.Scope.BackendId, out var adapter))
        {
            return false;
        }

        return adapter.GetCapabilityRow().ResumeCurrentlyUsable;
    }
}
