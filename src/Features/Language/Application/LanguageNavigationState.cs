namespace Zaide.Features.Language.Application;

/// <summary>Operational state for Go to Definition projection.</summary>
public enum LanguageNavigationState
{
    /// <summary>No definition work in flight.</summary>
    Idle,

    /// <summary>Definition request in flight.</summary>
    Loading,

    /// <summary>Exactly one valid location; navigation may proceed.</summary>
    Ready,

    /// <summary>Multiple valid locations; chooser must be shown before navigation.</summary>
    Choose,

    /// <summary>Successful response with zero locations.</summary>
    Empty,

    /// <summary>Session or capability not ready.</summary>
    Unavailable,

    /// <summary>Request failed.</summary>
    Failed,

    /// <summary>Request was cancelled or superseded.</summary>
    Cancelled,
}
