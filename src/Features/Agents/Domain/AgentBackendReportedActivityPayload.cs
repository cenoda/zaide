using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Normalized backend-reported activity emitted before terminal run completion.
/// </summary>
internal sealed class AgentBackendReportedActivityPayload : AgentEventPayload
{
    public AgentBackendReportedActivityPayload(
        AcpBackendActivityKind activityKind,
        string summary,
        string? acpCorrelationId = null)
    {
        if (!Enum.IsDefined(activityKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(activityKind),
                activityKind,
                "Activity kind is invalid.");
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("Activity summary is required.", nameof(summary));
        }

        ActivityKind = activityKind;
        Summary = summary;
        AcpCorrelationId = acpCorrelationId;
    }

    public AcpBackendActivityKind ActivityKind { get; }

    public string Summary { get; }

    public string? AcpCorrelationId { get; }
}
