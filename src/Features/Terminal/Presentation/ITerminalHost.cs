using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;

namespace Zaide.Features.Terminal.Presentation;

public interface ITerminalHost : IDisposable
{
    ObservableCollection<TerminalTabViewModel> Tabs { get; }
    TerminalTabViewModel? ActiveTab { get; }
    TerminalViewModel? ActiveSession { get; }
    IObservable<string?> StartupError { get; }
    Task EnsureActiveSessionStartedAsync();
    void FocusActiveSession();
    ReactiveCommand<Unit, Unit> NewTabCommand { get; }
    ReactiveCommand<TerminalTabViewModel, Unit> CloseTabCommand { get; }
    ReactiveCommand<TerminalTabViewModel, Unit> ActivateTabCommand { get; }
}
