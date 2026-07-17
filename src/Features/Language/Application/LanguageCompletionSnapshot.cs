using System;
using System.Collections.Generic;

namespace Zaide.Features.Language.Application;

/// <summary>
/// Immutable completion state for the active editor projection.
/// </summary>
public sealed record LanguageCompletionSnapshot(
    LanguageCompletionState State,
    long RequestId,
    long SessionGeneration,
    string? DocumentUri,
    string? FilePath,
    int DocumentVersion,
    int CaretOffset,
    int SelectedIndex,
    IReadOnlyList<LanguageCompletionItem> Items,
    string? FailureMessage)
{
    /// <summary>Idle snapshot with no popup.</summary>
    public static LanguageCompletionSnapshot Idle { get; } = new(
        LanguageCompletionState.Idle,
        RequestId: 0,
        SessionGeneration: 0,
        DocumentUri: null,
        FilePath: null,
        DocumentVersion: 0,
        CaretOffset: 0,
        SelectedIndex: 0,
        Items: Array.Empty<LanguageCompletionItem>(),
        FailureMessage: null);

    /// <summary>Whether the popup should be visible.</summary>
    public bool IsPopupOpen =>
        State == LanguageCompletionState.Ready && Items.Count > 0;
}
