using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Contracts.Transparency.Trace;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Trace;

namespace Zaide.Features.Agents.Application.Transparency.Trace;

/// <summary>
/// Application façade over the M2 trace evidence subsystem. Exposes the
/// capture sink, inspector, and source registry to the composition root,
/// presentation layer, and tests through one stable boundary. Backend
/// adapters never reach into the sink or store directly; they submit through
/// their registered <see cref="IAgentTraceBackendEvidenceSource"/>.
/// </summary>
internal sealed class AgentTraceCoordinator
{
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly AgentTraceCaptureSink _sink;
    private readonly IAgentTraceInspector _inspector;
    private readonly IAgentTraceSourceRegistry _sourceRegistry;
    private readonly AgentTraceBackendEvidenceSourceRegistryFilter _registryFilter;
    private readonly AgentDurableWorkspaceStorageKeyResolver _workspaceKeyResolver;

    public AgentTraceCoordinator(
        AgentTraceCaptureSink sink,
        IAgentTraceInspector inspector,
        IAgentTraceSourceRegistry sourceRegistry,
        AgentDurableWorkspaceStorageKeyResolver workspaceKeyResolver)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(sourceRegistry);
        ArgumentNullException.ThrowIfNull(workspaceKeyResolver);

        _sink = sink;
        _inspector = inspector;
        _sourceRegistry = sourceRegistry;
        _workspaceKeyResolver = workspaceKeyResolver;
        _registryFilter = new AgentTraceBackendEvidenceSourceRegistryFilter(sourceRegistry);
    }

    public AgentTraceCaptureSink Sink => _sink;

    public IAgentTraceInspector Inspector => _inspector;

    public IAgentTraceSourceRegistry Sources => _sourceRegistry;

    public bool IsCaptureEnabled() => _sink.IsCaptureEnabled();

    public void EnableCapture() => _sink.EnableCapture();

    public void DisableCapture() => _sink.DisableCapture();

    public long BackpressureDroppedCount => _sink.BackpressureDroppedCount;

    public long AdmittedCount => _sink.AdmittedCount;

    public long WrittenCount => _sink.WrittenCount;

    public AgentTraceCaptureResult TrySubmit(AgentTraceCaptureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_registryFilter.IsAllowed(request.BackendId))
        {
            return new AgentTraceCaptureResult(
                AgentTraceCaptureStatus.Disabled,
                captureState: AgentTraceCaptureState.Disabled,
                reason: "Backend is not a registered trace source.");
        }

        return _sink.TrySubmit(request);
    }

    public AgentTraceInspectionSummary GetSummary(string? workspaceRoot) =>
        _inspector.GetSummary(_workspaceKeyResolver.Resolve(workspaceRoot));

    public IReadOnlyList<AgentTraceRecord> GetRecords(
        string? workspaceRoot,
        long afterOrderingSequence,
        int maxRecords) =>
        _inspector.GetRecords(
            _workspaceKeyResolver.Resolve(workspaceRoot),
            afterOrderingSequence,
            maxRecords);

    public static string SerializeUnavailableMarker(string backendId) =>
        JsonSerializer.Serialize(
            new UnavailableMarker
            {
                BackendId = backendId,
                CapturedAtUtc = DateTimeOffset.UtcNow,
            },
            PayloadOptions);

    private sealed class UnavailableMarker
    {
        public string BackendId { get; set; } = string.Empty;

        public DateTimeOffset CapturedAtUtc { get; set; }
    }
}

/// <summary>
/// Composition-root filter that admits only registered backend evidence
/// sources. Prevents unverified or third-party code paths from injecting
/// trace evidence.
/// </summary>
internal sealed class AgentTraceBackendEvidenceSourceRegistryFilter
    : IAgentTraceBackendEvidenceSourceRegistryFilter
{
    private readonly IAgentTraceSourceRegistry _registry;

    public AgentTraceBackendEvidenceSourceRegistryFilter(IAgentTraceSourceRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public bool IsAllowed(string backendId) =>
        !string.IsNullOrWhiteSpace(backendId) && _registry.TryGet(backendId, out _);
}
