using System;

namespace Zaide.Features.Editor.Contracts;

/// <summary>
/// Marshals editor document mutations onto the UI thread.
/// </summary>
public interface IEditorUiDispatcher
{
    void Invoke(Action action);

    T Invoke<T>(Func<T> func);
}
