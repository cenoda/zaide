namespace Zaide.Features.Language.Application;

/// <summary>Operational state for active-document hover projection.</summary>
public enum LanguageHoverState
{
    /// <summary>No tooltip; idle.</summary>
    Idle,

    /// <summary>Request in flight.</summary>
    Loading,

    /// <summary>Hover content available.</summary>
    Ready,

    /// <summary>Successful response with no displayable content.</summary>
    Empty,

    /// <summary>Session or capability not ready.</summary>
    Unavailable,

    /// <summary>Request failed.</summary>
    Failed,

    /// <summary>Request was cancelled or superseded.</summary>
    Cancelled,
}
