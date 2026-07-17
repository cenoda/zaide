namespace Zaide.Features.Language.Application;

/// <summary>Terminal and in-flight states for whole-document formatting.</summary>
public enum LanguageFormattingState
{
    /// <summary>No formatting work is active.</summary>
    Idle,

    /// <summary>A formatting request is in flight.</summary>
    Loading,

    /// <summary>Valid edits produced a new document text ready to apply.</summary>
    Ready,

    /// <summary>Server returned no edits; document is already formatted.</summary>
    NoEdits,

    /// <summary>Session not ready or document not eligible.</summary>
    Unavailable,

    /// <summary>Server does not advertise document formatting.</summary>
    Unsupported,

    /// <summary>Request failed or returned a null/unparseable result.</summary>
    Failed,

    /// <summary>Request was cancelled or superseded.</summary>
    Cancelled,

    /// <summary>Edits were overlapping, out of range, or otherwise invalid.</summary>
    Invalid,

    /// <summary>Response no longer matches the active document/version/generation.</summary>
    Stale,
}
