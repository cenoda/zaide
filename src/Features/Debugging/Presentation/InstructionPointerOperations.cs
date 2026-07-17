using AvaloniaEdit;

namespace Zaide.Features.Debugging.Presentation;

/// <summary>
/// View-layer instruction-pointer margin host for the shared <see cref="TextEditor"/>.
/// </summary>
public sealed class InstructionPointerOperations : System.IDisposable
{
    private readonly TextEditor _textEditor;
    private InstructionPointerMargin? _margin;
    private bool _disposed;

    public InstructionPointerOperations(TextEditor textEditor)
    {
        _textEditor = textEditor ?? throw new System.ArgumentNullException(nameof(textEditor));
    }

    public bool IsInstalled => _margin is not null;

    public void Install()
    {
        if (_disposed || _margin is not null)
            return;

        _margin = new InstructionPointerMargin();
        _textEditor.TextArea.LeftMargins.Insert(0, _margin);
    }

    public void Clear()
    {
        if (_margin is null)
            return;

        _textEditor.TextArea.LeftMargins.Remove(_margin);
        _margin = null;
    }

    public void SetMarker(EditorInstructionPointerMarker? marker)
    {
        _margin?.SetMarker(marker);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Clear();
    }
}