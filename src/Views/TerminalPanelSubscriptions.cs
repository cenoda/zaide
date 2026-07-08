using System;
using System.Reactive.Disposables;
using Zaide.ViewModels;

namespace Zaide.Views;

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
