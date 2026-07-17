namespace Zaide.Features.Debugging.Application;

/// <summary>
/// Truthful load state for debug stack and variable projections.
/// </summary>
public enum DebugProjectionState
{
    Unavailable,
    Loading,
    Ready,
    Empty,
    Error,
}