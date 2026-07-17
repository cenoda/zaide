namespace Zaide.Features.Language.Application;

/// <summary>Operational state for document/workspace symbol surfaces.</summary>
public enum LanguageSymbolState
{
    /// <summary>No symbol surface open.</summary>
    Idle,

    /// <summary>Symbol request in flight.</summary>
    Loading,

    /// <summary>Symbols available for presentation.</summary>
    Ready,

    /// <summary>Successful response with zero symbols.</summary>
    Empty,

    /// <summary>Session or capability not ready.</summary>
    Unavailable,

    /// <summary>Request failed.</summary>
    Failed,

    /// <summary>Request was cancelled or superseded.</summary>
    Cancelled,
}
