using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Application.Transparency;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Contracts.Transparency.Trace;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Trace;
using Zaide.Features.Agents.Infrastructure.Acp;

namespace Zaide.Features.Agents.Application.Acp;

/// <summary>
/// ACP backend that advertises broker-mediated client filesystem capabilities.
/// </summary>
internal sealed class AcpActionCapableAgentBackend
    : IAgentBackend, IAgentActionRequestCapableBackend, IAgentCancellationAcknowledgementBackend
{
    private readonly AcpAgentSessionAdapter _sessionAdapter;
    private readonly IAgentTraceBackendEvidenceSource? _traceSource;
    private readonly AgentDurableWorkspaceStorageKeyResolver? _traceWorkspaceKeyResolver;
    private readonly Zaide.Features.Workspace.Contracts.IWorkspaceActionAuthority? _workspaceAuthority;
    private readonly object _capabilitySync = new();
    private AgentCapabilitySnapshot _capabilitySnapshot;

    public AcpActionCapableAgentBackend(
        IAcpSessionClientFactory clientFactory,
        Func<string> workingDirectoryProvider,
        IAgentActorBackendBindingStore? bindingStore = null,
        Zaide.Features.Workspace.Contracts.IWorkspaceActionAuthority? workspaceAuthority = null,
        IAgentTraceBackendEvidenceSource? traceSource = null,
        AgentDurableWorkspaceStorageKeyResolver? traceWorkspaceKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(workingDirectoryProvider);

        _sessionAdapter = new AcpAgentSessionAdapter(
            clientFactory,
            workingDirectoryProvider,
            bindingStore);
        _workspaceAuthority = workspaceAuthority;
        _traceSource = traceSource?.BackendId == AgentBackendIds.AcpValue ? traceSource : null;
        _traceWorkspaceKeyResolver = traceWorkspaceKeyResolver;
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

        TryCaptureTrace(context, AgentTraceKind.Request, "session/prompt", "outbound", context.Request.MessageText);

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

            TryCaptureTraceForEvent(context, backendEvent);

            yield return backendEvent;
        }
    }

    public Task<AgentCancellationAcknowledgementResult> AcknowledgeCancellationAsync(
        AgentSessionId sessionId,
        CancellationToken cancellationToken = default) =>
        _sessionAdapter.AcknowledgeCancellationAsync(sessionId, cancellationToken);

    private void TryCaptureTraceForEvent(
        AgentBackendExecutionContext context,
        AgentBackendEvent backendEvent)
    {
        switch (backendEvent.Payload)
        {
            case AgentBackendMessageCompletedPayload completed:
                TryCaptureTrace(context, AgentTraceKind.Response, "session/update", "inbound", completed.AssistantText);
                break;
            case AgentBackendFailurePayload failure:
                TryCaptureTrace(context, AgentTraceKind.Error, "session/error", "inbound", failure.Reason);
                break;
        }
    }

    private void TryCaptureTrace(
        AgentBackendExecutionContext context,
        AgentTraceKind kind,
        string method,
        string direction,
        string opaqueBody)
    {
        if (_traceSource is null
            || _traceWorkspaceKeyResolver is null
            || _workspaceAuthority?.TryCaptureCurrentScope(out var workspaceScope) != true)
        {
            return;
        }

        var capturedAtUtc = DateTimeOffset.UtcNow;
        _ = _traceSource.Submit(new AgentTraceCaptureRequest(
            _traceWorkspaceKeyResolver.Resolve(workspaceScope.RootPath),
            AgentBackendIds.AcpValue,
            kind,
            AgentTraceEvidenceLevel.BackendExecutedAndReported,
            AcpAgentTraceSource.SerializeProtocolFrame(
                AgentBackendIds.AcpValue,
                method,
                context.Request.RunId.ToString(),
                direction,
                capturedAtUtc,
                AgentTraceBackendEvidenceSourceWriter.ComputeOpaqueBodyMarker(opaqueBody)),
            new AgentTraceRecordScope(
                context.Request.ConversationId.ToString(),
                context.Request.SessionId.ToString(),
                context.Request.RunId.ToString(),
                AgentBackendIds.AcpValue),
            idempotencyKey: $"trace:acp:{context.Request.RunId}:{method}",
            capturedAtUtc: capturedAtUtc));
    }
}
