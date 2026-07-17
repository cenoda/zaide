using System.Reactive;
using ReactiveUI;

namespace Zaide.Features.Terminal.Presentation;

public class TerminalTabViewModel : ReactiveObject
{
    private string? _title;
    private bool _isActive;

    public string? Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    public TerminalViewModel Session { get; }

    public bool IsActive
    {
        get => _isActive;
        internal set => this.RaiseAndSetIfChanged(ref _isActive, value);
    }

    internal TerminalTabViewModel(TerminalViewModel session)
    {
        Session = session;
        Title = "Terminal";
    }
}
