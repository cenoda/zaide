using System;

namespace Zaide.Views;

internal sealed class FinalWindowCleanup : IDisposable
{
    private readonly Action _disposeEditor;
    private readonly Action _disposeTerminal;
    private bool _disposed;

    public FinalWindowCleanup(Action disposeEditor, Action disposeTerminal)
    {
        _disposeEditor = disposeEditor;
        _disposeTerminal = disposeTerminal;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _disposeEditor();
        _disposeTerminal();
    }
}
