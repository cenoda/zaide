using System;
using System.Collections.Generic;
using System.Linq;
using AvaloniaEdit;
using AvaloniaEdit.Folding;
using Zaide.App.Composition;
using Zaide.App.Shell;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Editor.Contracts;

namespace Zaide.Features.Editor.Presentation;

/// <summary>
/// View-layer implementation of <see cref="IFoldingOperations"/> that wraps
/// a single <see cref="TextEditor"/>'s <c>FoldingManager</c> and
/// <c>FoldingMargin</c>. Uses <see cref="BraceFoldingStrategy"/> to discover
/// foldable regions and converts <see cref="BraceRegion"/> values to
/// AvaloniaEdit <see cref="NewFolding"/> objects.
/// </summary>
/// <remarks>
/// <para>
/// Because <c>MainWindow</c> reuses one <c>TextEditor</c> across all tabs,
/// this instance is created once and <see cref="Clear"/> must be called on
/// every active-tab switch before <see cref="Install"/> is called with the
/// new tab's text.
/// </para>
/// <para>
/// After every fold/unfold operation, the caret is brought into view via
/// <c>BringCaretToView()</c> to satisfy the deterministic-fallback contract.
/// </para>
/// </remarks>
public sealed class FoldingOperations : IFoldingOperations, IDisposable
{
    private readonly TextEditor _textEditor;
    private FoldingManager? _foldingManager;
    private FoldingMargin? _foldingMargin;
    private IReadOnlyList<BraceRegion> _regions = Array.Empty<BraceRegion>();
    private bool _disposed;

    public FoldingOperations(TextEditor textEditor)
    {
        _textEditor = textEditor ?? throw new ArgumentNullException(nameof(textEditor));
    }

    /// <inheritdoc />
    public bool IsAvailable => _foldingManager is not null;

    /// <inheritdoc />
    public void Install(string text)
    {
        if (_disposed) return;

        // Ensure FoldingManager and margin are installed.
        if (_foldingManager is null)
        {
            _foldingManager = FoldingManager.Install(_textEditor.TextArea);

            _foldingMargin = new FoldingMargin();
            _textEditor.TextArea.LeftMargins.Add(_foldingMargin);
        }

        // Explicitly clear any folding sections from the previous tab
        // before installing new ones. Per M0 contract: folding state
        // must never leak between tabs.
        _foldingManager.Clear();

        // Discover regions and convert to NewFolding objects.
        _regions = BraceFoldingStrategy.Discover(text);

        var newFoldings = new List<NewFolding>(_regions.Count);
        foreach (var region in _regions)
        {
            newFoldings.Add(new NewFolding(region.StartOffset, region.EndOffset)
            {
                Name = region.Title,
                DefaultClosed = false,
                IsDefinition = region.Depth == 0
            });
        }

        _foldingManager.UpdateFoldings(newFoldings, -1);
    }

    /// <inheritdoc />
    public void Clear()
    {
        if (_foldingManager is not null)
        {
            _foldingManager.Clear();
        }

        _regions = Array.Empty<BraceRegion>();

        // Remove and dispose the folding margin.
        if (_foldingMargin is not null)
        {
            _textEditor.TextArea.LeftMargins.Remove(_foldingMargin);
            _foldingMargin = null;
        }

        // Uninstall the FoldingManager so it doesn't hold stale references.
        if (_foldingManager is not null)
        {
            FoldingManager.Uninstall(_foldingManager);
            _foldingManager = null;
        }
    }

    /// <inheritdoc />
    public bool ToggleCurrent()
    {
        if (_foldingManager is null || _regions.Count == 0)
            return false;

        var caretOffset = _textEditor.TextArea.Caret.Offset;

        // Find the innermost brace region containing the caret.
        var target = BraceFoldingStrategy.FindInnermostContaining(_regions, caretOffset);
        if (target is null)
            return false;

        // Find the corresponding FoldingSection.
        // FoldingManager.GetFoldingsContaining returns sections that contain the offset.
        var containing = _foldingManager.GetFoldingsContaining(caretOffset);
        FoldingSection? targetSection = null;

        foreach (var section in containing)
        {
            // Match by start/end offset to the BraceRegion.
            if (section.StartOffset == target.StartOffset)
            {
                targetSection = section;
                break;
            }
        }

        if (targetSection is null)
        {
            // Fallback: try GetFoldingsAt on the caret offset.
            var atCaret = _foldingManager.GetFoldingsAt(caretOffset);
            foreach (var section in atCaret)
            {
                if (section.StartOffset == target.StartOffset)
                {
                    targetSection = section;
                    break;
                }
            }
        }

        if (targetSection is null)
            return false;

        targetSection.IsFolded = !targetSection.IsFolded;
        _textEditor.TextArea.Caret.BringCaretToView();
        return true;
    }

    /// <inheritdoc />
    public void FoldAll()
    {
        if (_foldingManager is null) return;

        foreach (var section in _foldingManager.AllFoldings)
        {
            section.IsFolded = true;
        }

        _textEditor.TextArea.Caret.BringCaretToView();
    }

    /// <inheritdoc />
    public void UnfoldAll()
    {
        if (_foldingManager is null) return;

        foreach (var section in _foldingManager.AllFoldings)
        {
            section.IsFolded = false;
        }

        _textEditor.TextArea.Caret.BringCaretToView();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Clear();
    }
}
