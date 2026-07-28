using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Application.Acp;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Infrastructure.Acp;

/// <summary>
/// Phase 20 ACP backend adapter over the Phase 15 session boundary.
/// </summary>
internal sealed class AcpAgentBackend : IAgentBackend
{
    internal const string BackendVersionValue = "zaide-acp/1";

    private readonly AcpAgentSessionAdapter _sessionAdapter;
    private readonly object _capabilitySync = new();
    private AgentCapabilitySnapshot _capabilitySnapshot;

    public AcpAgentBackend(Func<CancellationToken, Task<IAcpSessionClient>> clientFactory)
        : this(clientFactory, () => "/tmp/zaide-acp")
    {
    }

    internal AcpAgentBackend(
        Func<CancellationToken, Task<IAcpSessionClient>> clientFactory,
        Func<string> workingDirectoryProvider)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(workingDirectoryProvider);

        _sessionAdapter = new AcpAgentSessionAdapter(clientFactory, workingDirectoryProvider);
        _capabilitySnapshot = AcpCapabilitySnapshotMapper.CreateInitialSnapshot();
    }

    public AgentBackendId BackendId => AgentBackendIds.Acp;

    public string BackendVersion => BackendVersionValue;

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
}
