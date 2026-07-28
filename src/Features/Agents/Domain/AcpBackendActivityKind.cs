namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Zaide-owned taxonomy for ACP backend-reported structured activity.
/// </summary>
internal enum AcpBackendActivityKind
{
    ToolCall,
    ToolCallUpdate,
    Plan,
    UsageUpdate,
    SessionControlUpdate,
    UnknownUpdate,
}
