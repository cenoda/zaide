using System;

namespace Zaide.Features.Language.Application;

/// <summary>
/// Immutable hover state for the active editor projection.
/// </summary>
public sealed record LanguageHoverSnapshot(
    LanguageHoverState State,
    long RequestId,
    long SessionGeneration,
    string? DocumentUri,
    string? FilePath,
    int DocumentVersion,
    int CaretOffset,
    string? Content,
    string? FailureMessage)
{
    /// <summary>Idle snapshot with no tooltip.</summary>
    public static LanguageHoverSnapshot Idle { get; } = new(
        LanguageHoverState.Idle,
        RequestId: 0,
        SessionGeneration: 0,
        DocumentUri: null,
        FilePath: null,
        DocumentVersion: 0,
        CaretOffset: 0,
        Content: null,
        FailureMessage: null);

    /// <summary>Whether a tooltip should be visible.</summary>
    public bool IsVisible =>
        State == LanguageHoverState.Ready && !string.IsNullOrWhiteSpace(Content);
}
