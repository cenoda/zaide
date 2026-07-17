using System;
using System.Collections.Generic;

namespace Zaide.Features.Language.Application;

/// <summary>
/// Immutable Go to Definition state for editor/command projection.
/// </summary>
public sealed record LanguageNavigationSnapshot(
    LanguageNavigationState State,
    long RequestId,
    long SessionGeneration,
    string? SourceDocumentUri,
    string? SourceFilePath,
    int SourceDocumentVersion,
    int CaretOffset,
    int SelectedIndex,
    IReadOnlyList<LanguageLocation> Locations,
    string? FeedbackMessage)
{
    /// <summary>Idle snapshot with no navigation surface.</summary>
    public static LanguageNavigationSnapshot Idle { get; } = new(
        LanguageNavigationState.Idle,
        RequestId: 0,
        SessionGeneration: 0,
        SourceDocumentUri: null,
        SourceFilePath: null,
        SourceDocumentVersion: 0,
        CaretOffset: 0,
        SelectedIndex: 0,
        Locations: Array.Empty<LanguageLocation>(),
        FeedbackMessage: null);

    /// <summary>Whether the multi-result chooser should be visible.</summary>
    public bool IsChooserOpen =>
        State == LanguageNavigationState.Choose && Locations.Count > 1;

    /// <summary>Whether a single location is ready for immediate navigation.</summary>
    public bool IsSingleNavigateReady =>
        State == LanguageNavigationState.Ready && Locations.Count == 1;
}
