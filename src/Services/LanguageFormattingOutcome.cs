using System;
using System.Collections.Generic;

namespace Zaide.Services;

/// <summary>
/// Result of a single whole-document formatting attempt for callers that
/// apply text (explicit format command or Format on Save).
/// </summary>
/// <param name="Kind">Outcome classification.</param>
/// <param name="FormattedText">
/// Accepted full document text when <see cref="Kind"/> is
/// <see cref="LanguageFormattingOutcomeKind.Applied"/> or
/// <see cref="LanguageFormattingOutcomeKind.NoEdits"/> (same as source).
/// Null for every non-accepted kind.
/// </param>
/// <param name="Edits">Validated edits when applied; empty otherwise.</param>
/// <param name="FeedbackMessage">Truthful user-facing message when useful.</param>
public sealed record LanguageFormattingOutcome(
    LanguageFormattingOutcomeKind Kind,
    string? FormattedText,
    IReadOnlyList<LanguageTextEdit> Edits,
    string? FeedbackMessage)
{
    /// <summary>True when formatting produced a usable full-document text.</summary>
    public bool IsAccepted =>
        Kind is LanguageFormattingOutcomeKind.Applied or LanguageFormattingOutcomeKind.NoEdits;

    /// <summary>True when the document text should change.</summary>
    public bool HasTextChange =>
        Kind == LanguageFormattingOutcomeKind.Applied && FormattedText is not null;

    /// <summary>Factory for non-accepted outcomes.</summary>
    public static LanguageFormattingOutcome Terminal(
        LanguageFormattingOutcomeKind kind,
        string? feedbackMessage) =>
        new(kind, null, Array.Empty<LanguageTextEdit>(), feedbackMessage);
}

/// <summary>Classification of a formatting attempt for apply/save callers.</summary>
public enum LanguageFormattingOutcomeKind
{
    /// <summary>Valid empty edit list; document unchanged.</summary>
    NoEdits,

    /// <summary>Valid edits applied to produce a new full document text.</summary>
    Applied,

    /// <summary>Session/document not ready.</summary>
    Unavailable,

    /// <summary>Server capability missing.</summary>
    Unsupported,

    /// <summary>Transport/protocol failure.</summary>
    Failed,

    /// <summary>Cancelled or superseded.</summary>
    Cancelled,

    /// <summary>Malformed, overlapping, or out-of-range edits.</summary>
    Invalid,

    /// <summary>Stale generation/version/active-document identity.</summary>
    Stale,
}
