using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure.Acp;

namespace Zaide.Features.Agents.Application.Acp;

/// <summary>
/// ACP backend that advertises broker-mediated client filesystem capabilities.
/// </summary>
internal sealed class AcpActionCapableAgentBackend
    : IAgentBackend, IAgentActionRequestCapableBackend, IAgentCancellationAcknowledgementBackend
{
    private readonly AcpAgentSessionAdapter _sessionAdapter;
    private readonly object _capabilitySync = new();
    private AgentCapabilitySnapshot _capabilitySnapshot;

    public AcpActionCapableAgentBackend(
        IAcpSessionClientFactory clientFactory,
        Func<string> workingDirectoryProvider,
        IAgentActorBackendBindingStore? bindingStore = null)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(workingDirectoryProvider);

        _sessionAdapter = new AcpAgentSessionAdapter(
            clientFactory,
            workingDirectoryProvider,
            bindingStore);
        _capabilitySnapshot = AcpCapabilitySnapshotMapper.CreateInitialSnapshot();
    }

    public AgentBackendId BackendId => AgentBackendIds.Acp;

    public string BackendVersion => AcpAgentBackend.BackendVersionValue;

    public AgentCapabilitySnapshot CapabilitySnapshot
    {
        get
        {
            lock (_capabilitySync)
            {
                return _capabilitySnapshot;
            }
        }
    }

    public async IAsyncEnumerable<AgentBackendEvent> ExecuteAsync(
        AgentBackendExecutionContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        AgentCapabilitySnapshot snapshot;
        lock (_capabilitySync)
        {
            snapshot = _capabilitySnapshot;
        }

        await foreach (var backendEvent in _sessionAdapter.ExecuteAsync(
                           context,
                           snapshot,
                           enableActionBridge: true,
                           cancellationToken).ConfigureAwait(false))
        {
            if (backendEvent.Kind == AgentBackendEventKind.CapabilitySnapshotChanged
                && backendEvent.Payload is AgentBackendCapabilityChangedPayload capabilityPayload)
            {
                lock (_capabilitySync)
                {
                    _capabilitySnapshot = capabilityPayload.Snapshot;
                }
            }

            yield return backendEvent;
        }
    }

    public Task<AgentCancellationAcknowledgementResult> AcknowledgeCancellationAsync(
        AgentSessionId sessionId,
        CancellationToken cancellationToken = default) =>
        _sessionAdapter.AcknowledgeCancellationAsync(sessionId, cancellationToken);
}
