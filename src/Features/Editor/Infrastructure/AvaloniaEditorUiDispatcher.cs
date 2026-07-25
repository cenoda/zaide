using System;
using Avalonia.Threading;
using Zaide.Features.Editor.Contracts;

namespace Zaide.Features.Editor.Infrastructure;

/// <summary>
/// Production UI-thread dispatcher for editor document reconciliation.
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
}
