using System;
using System.Collections.Generic;

namespace Zaide.Features.Language.Application;

/// <summary>
/// Immutable snapshot of whole-document formatting ownership.
/// </summary>
public sealed record LanguageFormattingSnapshot(
    LanguageFormattingState State,
    long RequestId,
    long SessionGeneration,
    string DocumentUri,
    string? FilePath,
    int DocumentVersion,
    IReadOnlyList<LanguageTextEdit> Edits,
    string? FormattedText,
    string? FeedbackMessage)
{
    /// <summary>Idle empty snapshot.</summary>
    public static LanguageFormattingSnapshot Idle { get; } = new(
        LanguageFormattingState.Idle,
        RequestId: 0,
        SessionGeneration: 0,
        DocumentUri: string.Empty,
        FilePath: null,
        DocumentVersion: 0,
        Edits: Array.Empty<LanguageTextEdit>(),
        FormattedText: null,
        FeedbackMessage: null);

    /// <summary>
    /// True when <see cref="FormattedText"/> is accepted and safe to apply
    /// (including the no-op case where text equals the source).
    /// </summary>
    public bool IsAccepted =>
        State is LanguageFormattingState.Ready or LanguageFormattingState.NoEdits;
}
