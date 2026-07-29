using System;
using System.Collections.Generic;
using Zaide.Features.Agents.Contracts.Transparency.Usage;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Usage;

namespace Zaide.Features.Agents.Application.Transparency.Usage;

internal sealed class AgentUsageCoordinator
{
    private readonly AgentUsageCaptureSink _sink;
    private readonly IAgentUsageInspector _inspector;
    private readonly AgentDurableWorkspaceStorageKeyResolver _workspaceKeyResolver;

    public AgentUsageCoordinator(
        AgentUsageCaptureSink sink,
        IAgentUsageInspector inspector,
        AgentDurableWorkspaceStorageKeyResolver workspaceKeyResolver)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(workspaceKeyResolver);

        _sink = sink;
        _inspector = inspector;
        _workspaceKeyResolver = workspaceKeyResolver;
    }

    public AgentUsageCaptureSink Sink => _sink;

    public IAgentUsageInspector Inspector => _inspector;

    public bool IsCaptureEnabled() => _sink.IsCaptureEnabled();

    public void EnableCapture() => _sink.EnableCapture();

    public void DisableCapture() => _sink.DisableCapture();

    public AgentUsageCaptureResult TrySubmit(AgentUsageCaptureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _sink.TrySubmit(request);
    }

    public AgentUsageInspectionSummary GetSummary(string? workspaceRoot) =>
        _inspector.GetSummary(_workspaceKeyResolver.Resolve(workspaceRoot));

    public IReadOnlyList<AgentUsageRecord> GetRecords(
        string? workspaceRoot,
        long afterOrderingSequence,
        int maxRecords) =>
        _inspector.GetRecords(
            _workspaceKeyResolver.Resolve(workspaceRoot),
            afterOrderingSequence,
            maxRecords);
}
