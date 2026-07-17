using System;
using System.Reactive.Disposables;

namespace Zaide.Features.Terminal.Presentation;

internal static class TerminalPanelSubscriptions
{
    public static IDisposable SubscribeToRestarted(TerminalViewModel? session, Action handler)
    {
        if (session is null)
        {
            return Disposable.Empty;
        }

        session.Restarted += handler;
        return Disposable.Create(() => session.Restarted -= handler);
    }
}
