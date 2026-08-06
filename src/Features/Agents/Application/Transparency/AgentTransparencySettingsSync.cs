using System;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Application.Transparency.Usage;
using Zaide.Features.Agents.Presentation.Transparency;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Domain;

namespace Zaide.Features.Agents.Application.Transparency;

/// <summary>
/// Applies durable agent/transparency settings from <see cref="ISettingsService"/>
/// to runtime capture sinks and refreshes availability projections.
/// </summary>
internal sealed class AgentTransparencySettingsSync : IDisposable
{
    private readonly ISettingsService _settings;
    private readonly AgentTraceCaptureSink _traceSink;
    private readonly AgentUsageCaptureSink _usageSink;
    private readonly AgentTraceAvailabilityProjection _traceAvailability;
    private readonly AgentUsageAvailabilityProjection _usageAvailability;
    private readonly IDisposable _subscription;
    private bool _disposed;

    public AgentTransparencySettingsSync(
        ISettingsService settings,
        AgentTraceCaptureSink traceSink,
        AgentUsageCaptureSink usageSink,
        AgentTraceAvailabilityProjection traceAvailability,
        AgentUsageAvailabilityProjection usageAvailability)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _traceSink = traceSink ?? throw new ArgumentNullException(nameof(traceSink));
        _usageSink = usageSink ?? throw new ArgumentNullException(nameof(usageSink));
        _traceAvailability = traceAvailability
            ?? throw new ArgumentNullException(nameof(traceAvailability));
        _usageAvailability = usageAvailability
            ?? throw new ArgumentNullException(nameof(usageAvailability));

        Apply(_settings.Current);
        _subscription = _settings.WhenChanged.Subscribe(Apply);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _subscription.Dispose();
    }

    private void Apply(SettingsModel snapshot)
    {
        var agents = snapshot.Agents;
        _traceSink.ApplyCaptureEnabled(agents.TraceCaptureEnabled);
        _usageSink.ApplyCaptureEnabled(agents.UsageCaptureEnabled);
        _traceAvailability.Refresh(force: true);
        _usageAvailability.Refresh(force: true);
    }
}
