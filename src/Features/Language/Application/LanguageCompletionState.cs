namespace Zaide.Features.Language.Application;

/// <summary>Operational state for active-document completion projection.</summary>
public enum LanguageCompletionState
{
    /// <summary>No popup; idle.</summary>
    Idle,

    /// <summary>Request in flight.</summary>
    Loading,

    /// <summary>Items available for presentation.</summary>
    Ready,

    /// <summary>Successful response with zero items.</summary>
    Empty,

    /// <summary>Session or capability not ready.</summary>
    Unavailable,

    /// <summary>Request failed.</summary>
    Failed,

    /// <summary>Request was cancelled or superseded.</summary>
    Cancelled,
}
