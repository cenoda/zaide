using System;
using System.Collections.Generic;

namespace Zaide.Services;

/// <summary>
/// Locked M5 policies for document/workspace symbol commands, debounce, and ordering.
/// </summary>
public static class LanguageSymbolPolicy
{
    /// <summary>Active-document outline command.</summary>
    public const string DocumentSymbolCommandId = "editor.documentSymbol";

    /// <summary>Workspace symbol query command.</summary>
    public const string WorkspaceSymbolCommandId = "workbench.symbol";

    /// <summary>Default gesture for document symbols (VS Code-compatible).</summary>
    public static IReadOnlyList<string> DocumentSymbolDefaultGestures { get; } = new[] { "Ctrl+Shift+O" };

    /// <summary>Default gesture for workspace symbols (VS Code-compatible).</summary>
    public static IReadOnlyList<string> WorkspaceSymbolDefaultGestures { get; } = new[] { "Ctrl+T" };

    /// <summary>
    /// Debounce before issuing <c>workspace/symbol</c> after a query change.
    /// Document symbols request immediately (no debounce).
    /// </summary>
    public static readonly TimeSpan WorkspaceQueryDebounce = TimeSpan.FromMilliseconds(200);

    public const string DocumentUnavailableMessage = "Document symbols are not available.";
    public const string WorkspaceUnavailableMessage = "Workspace symbols are not available.";
    public const string DocumentEmptyMessage = "No symbols in this document.";
    public const string WorkspaceEmptyMessage = "No workspace symbols match.";
    public const string DocumentFailedMessage = "Document symbols failed.";
    public const string WorkspaceFailedMessage = "Workspace symbols failed.";
}
