using System;
using Zaide.Features.Editor.Contracts;

namespace Zaide.Tests.Infrastructure;

/// <summary>
/// Executes editor UI dispatch synchronously for unit tests.
/// </summary>
internal sealed class SynchronousEditorUiDispatcher : IEditorUiDispatcher
{
    public void Invoke(Action action) => action();

    public T Invoke<T>(Func<T> func) => func();
}
