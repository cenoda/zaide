namespace Zaide.Services;

/// <summary>Structured failure kinds for breakpoint mutations.</summary>
public enum BreakpointOutcomeKind
{
    /// <summary>No workspace root is loaded.</summary>
    NoWorkspace,

    /// <summary>The requested line is not a valid one-based source line.</summary>
    InvalidLine,

    /// <summary>The requested breakpoint does not exist.</summary>
    NotFound,
}