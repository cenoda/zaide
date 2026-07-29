using System;
using System.Threading.Tasks;
using Zaide.Features.Agents.Application.Memory;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Presentation.Memory;
using Zaide.Features.Agents.Presentation.Transparency;

namespace Zaide.Features.Agents.Presentation.Transparency;

/// <summary>
/// Integrated transparency and memory management surface for Townhall/Agents UI.
/// </summary>
internal sealed class AgentTransparencyManagementViewModel
{
    public const string AutomationName = "Agent transparency and memory management";
    public const string AutomationHelpText =
        "Inspect trace evidence, usage and cost, session continuity, and durable memory. " +
        "Keyboard navigation and screen-reader-compatible bounded history paging are supported.";

    public const int DefaultPageSize = 64;
    public const int MaxPageSize = 256;

    private readonly AgentTraceInspectionViewModel _traceInspection;
    private readonly AgentUsageInspectionViewModel _usageInspection;
    private readonly AgentSessionContinuityInspectionViewModel _continuityInspection;
    private readonly AgentMemoryInspectionViewModel _memoryInspection;
    private readonly IAgentTransparencyLifecycleCoordinator _lifecycleCoordinator;
    private readonly AgentMemoryCoordinator _memoryCoordinator;
    private readonly AgentTraceAvailabilityProjection _traceAvailabilityProjection;
    private readonly AgentUsageAvailabilityProjection _usageAvailabilityProjection;
    private readonly AgentSessionContinuityAvailabilityProjection _continuityAvailabilityProjection;
    private readonly AgentMemoryAvailabilityProjection _memoryAvailabilityProjection;

    public AgentTransparencyManagementViewModel(
        AgentTraceInspectionViewModel traceInspection,
        AgentUsageInspectionViewModel usageInspection,
        AgentSessionContinuityInspectionViewModel continuityInspection,
        AgentMemoryInspectionViewModel memoryInspection,
        IAgentTransparencyLifecycleCoordinator lifecycleCoordinator,
        AgentMemoryCoordinator memoryCoordinator,
        AgentTraceAvailabilityProjection traceAvailabilityProjection,
        AgentUsageAvailabilityProjection usageAvailabilityProjection,
        AgentSessionContinuityAvailabilityProjection continuityAvailabilityProjection,
        AgentMemoryAvailabilityProjection memoryAvailabilityProjection)
    {
        _traceInspection = traceInspection ?? throw new ArgumentNullException(nameof(traceInspection));
        _usageInspection = usageInspection ?? throw new ArgumentNullException(nameof(usageInspection));
        _continuityInspection = continuityInspection
            ?? throw new ArgumentNullException(nameof(continuityInspection));
        _memoryInspection = memoryInspection ?? throw new ArgumentNullException(nameof(memoryInspection));
        _lifecycleCoordinator = lifecycleCoordinator
            ?? throw new ArgumentNullException(nameof(lifecycleCoordinator));
        _memoryCoordinator = memoryCoordinator ?? throw new ArgumentNullException(nameof(memoryCoordinator));
        _traceAvailabilityProjection = traceAvailabilityProjection
            ?? throw new ArgumentNullException(nameof(traceAvailabilityProjection));
        _usageAvailabilityProjection = usageAvailabilityProjection
            ?? throw new ArgumentNullException(nameof(usageAvailabilityProjection));
        _continuityAvailabilityProjection = continuityAvailabilityProjection
            ?? throw new ArgumentNullException(nameof(continuityAvailabilityProjection));
        _memoryAvailabilityProjection = memoryAvailabilityProjection
            ?? throw new ArgumentNullException(nameof(memoryAvailabilityProjection));
    }

    public string AccessibilityName => AutomationName;

    public string AccessibilityHelpText => AutomationHelpText;

    public AgentTraceAvailabilityProjection TraceAvailability => _traceAvailabilityProjection;

    public AgentUsageAvailabilityProjection UsageAvailability => _usageAvailabilityProjection;

    public AgentSessionContinuityAvailabilityProjection ContinuityAvailability =>
        _continuityAvailabilityProjection;

    public AgentMemoryAvailabilityProjection MemoryAvailability => _memoryAvailabilityProjection;

    public Task<AgentTransparencyExportPackage> ExportAllAsync(string? workspaceRoot = null)
    {
        var workspaceKey = _memoryCoordinator.ResolveWorkspaceKey(workspaceRoot);
        return Task.FromResult(_lifecycleCoordinator.Export(workspaceKey));
    }

    public Task<AgentTransparencyBackupPackage> BackupAsync(string? workspaceRoot = null)
    {
        var workspaceKey = _memoryCoordinator.ResolveWorkspaceKey(workspaceRoot);
        return Task.FromResult(_lifecycleCoordinator.Backup(workspaceKey));
    }

    public int ClampPageSize(int requestedPageSize) =>
        requestedPageSize <= 0
            ? DefaultPageSize
            : Math.Min(requestedPageSize, MaxPageSize);
}
