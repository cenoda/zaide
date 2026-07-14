using System;
using System.Collections.Generic;
using AvaloniaEdit;
using Zaide.ViewModels;

namespace Zaide.Views;

/// <summary>
/// View-layer breakpoint margin host for the shared <see cref="TextEditor"/>.
/// </summary>
public sealed class BreakpointOperations : IDisposable
{
    private readonly TextEditor _textEditor;
    private readonly Action<int> _toggleLine;
    private BreakpointMargin? _margin;
    private bool _disposed;

    public BreakpointOperations(TextEditor textEditor, Action<int> toggleLine)
    {
        _textEditor = textEditor ?? throw new ArgumentNullException(nameof(textEditor));
        _toggleLine = toggleLine ?? throw new ArgumentNullException(nameof(toggleLine));
    }

    public bool IsInstalled => _margin is not null;

    public void Install()
    {
        if (_disposed)
            return;

        if (_margin is null)
        {
            _margin = new BreakpointMargin(_toggleLine);
            _textEditor.TextArea.LeftMargins.Insert(0, _margin);
        }
    }

    public void Clear()
    {
        if (_margin is not null)
        {
            _textEditor.TextArea.LeftMargins.Remove(_margin);
            _margin = null;
        }
    }

    public void SetMarkers(IReadOnlyList<EditorBreakpointMarker> markers)
    {
        _margin?.SetMarkers(markers);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Clear();
    }
}