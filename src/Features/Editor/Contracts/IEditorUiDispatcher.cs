using System;

namespace Zaide.Features.Editor.Contracts;

/// <summary>
/// Marshals editor UI work onto the UI thread.
/// </summary>
public interface IEditorUiDispatcher
{
    /// <summary>
    /// Runs <paramref name="action"/> on the UI thread, blocking the caller until complete.
    /// Prefer <see cref="Post"/> for fire-and-forget projection work.
    /// </summary>
    void Invoke(Action action);

    /// <summary>
    /// Runs <paramref name="func"/> on the UI thread and returns its result, blocking the caller.
    /// </summary>
    T Invoke<T>(Func<T> func);

    /// <summary>
    /// Queues <paramref name="action"/> on the UI thread without blocking the caller.
    /// </summary>
    void Post(Action action);
}
