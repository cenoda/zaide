using System;
using Avalonia.Threading;
using Zaide.Features.Editor.Contracts;

namespace Zaide.Features.Editor.Infrastructure;

/// <summary>
/// Production UI-thread dispatcher for editor document reconciliation and language projection.
/// </summary>
internal sealed class AvaloniaEditorUiDispatcher : IEditorUiDispatcher
{
    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Invoke(action);
    }

    public T Invoke<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        if (Dispatcher.UIThread.CheckAccess())
        {
            return func();
        }

        return Dispatcher.UIThread.Invoke(func);
    }

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        // Always queue so publishers (including UI-thread caret/scroll traffic) never
        // re-enter apply work synchronously and latest-wins coalescing can batch.
        Dispatcher.UIThread.Post(action);
    }
}
