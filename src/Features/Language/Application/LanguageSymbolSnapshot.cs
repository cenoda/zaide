using System;
using System.Collections.Generic;
using Zaide.Features.Editor.Domain;

namespace Zaide.Features.Language.Application;

/// <summary>
/// Immutable document or workspace symbol surface state.
/// </summary>
public sealed record LanguageSymbolSnapshot(
    LanguageSymbolState State,
    LanguageSymbolScope Scope,
    long RequestId,
    long SessionGeneration,
    string? DocumentUri,
    string? FilePath,
    int DocumentVersion,
    string Query,
    int SelectedIndex,
    IReadOnlyList<LanguageSymbol> Symbols,
    string? FeedbackMessage)
{
    /// <summary>Idle snapshot with no symbol surface.</summary>
    public static LanguageSymbolSnapshot Idle { get; } = new(
        LanguageSymbolState.Idle,
        LanguageSymbolScope.None,
        RequestId: 0,
        SessionGeneration: 0,
        DocumentUri: null,
        FilePath: null,
        DocumentVersion: 0,
        Query: string.Empty,
        SelectedIndex: 0,
        Symbols: Array.Empty<LanguageSymbol>(),
        FeedbackMessage: null);

    /// <summary>Whether a symbol list surface should be visible.</summary>
    public bool IsSurfaceOpen =>
        State is LanguageSymbolState.Loading or LanguageSymbolState.Ready or LanguageSymbolState.Empty
        && Scope != LanguageSymbolScope.None;
}

/// <summary>Which symbol surface produced the snapshot.</summary>
public enum LanguageSymbolScope
{
    /// <summary>No active symbol surface.</summary>
    None,

    /// <summary>Active-document symbols.</summary>
    Document,

    /// <summary>Workspace-wide query-driven symbols.</summary>
    Workspace,
}
