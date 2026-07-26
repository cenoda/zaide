using System;
using System.Collections.Generic;
using System.Linq;

namespace Zaide.Features.Editor.Application;

/// <summary>
/// Immutable, read-only editor state for IDE context assembly. Excludes
/// presentation types and does not trigger file I/O or other side effects.
/// </summary>
public sealed class EditorStateSnapshot
{
    public static readonly IReadOnlyList<string> EmptyOpenFilePaths = Array.Empty<string>();

    public EditorStateSnapshot(
        long generation,
        string? activeFilePath = null,
        string? activeFileContent = null,
        bool activeFileIsDirty = false,
        IReadOnlyList<string>? openFilePaths = null,
        int caretLine = 1,
        int caretColumn = 1,
        int selectionStart = 0,
        int selectionLength = 0,
        string? selectionText = null)
    {
        if (generation < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(generation),
                generation,
                "Generation cannot be negative.");
        }

        if (caretLine < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(caretLine),
                caretLine,
                "Caret line must be positive.");
        }

        if (caretColumn < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(caretColumn),
                caretColumn,
                "Caret column must be positive.");
        }

        if (selectionStart < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(selectionStart),
                selectionStart,
                "Selection start cannot be negative.");
        }

        if (selectionLength < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(selectionLength),
                selectionLength,
                "Selection length cannot be negative.");
        }

        Generation = generation;
        ActiveFilePath = activeFilePath;
        ActiveFileContent = activeFileContent;
        ActiveFileIsDirty = activeFileIsDirty;
        OpenFilePaths = CopyOpenFilePaths(openFilePaths);
        CaretLine = caretLine;
        CaretColumn = caretColumn;
        SelectionStart = selectionStart;
        SelectionLength = selectionLength;
        SelectionText = selectionText;
    }

    public long Generation { get; }

    public string? ActiveFilePath { get; }

    public string? ActiveFileContent { get; }

    public bool ActiveFileIsDirty { get; }

    public IReadOnlyList<string> OpenFilePaths { get; }

    public int CaretLine { get; }

    public int CaretColumn { get; }

    public int SelectionStart { get; }

    public int SelectionLength { get; }

    public string? SelectionText { get; }

    private static IReadOnlyList<string> CopyOpenFilePaths(IReadOnlyList<string>? openFilePaths)
    {
        if (openFilePaths is null || openFilePaths.Count == 0)
        {
            return EmptyOpenFilePaths;
        }

        return Array.AsReadOnly(openFilePaths.ToArray());
    }
}
