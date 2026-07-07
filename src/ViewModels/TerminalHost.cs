using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using Zaide.Services;

namespace Zaide.ViewModels;

public sealed class TerminalHost : ReactiveObject, ITerminalHost
{
    private readonly ITerminalSessionFactory _factory;
    private readonly ObservableCollection<TerminalTabViewModel> _tabs;
    private TerminalTabViewModel? _activeTab;
    private readonly IObservable<string?> _startupError;
    private bool _disposed;

    public ObservableCollection<TerminalTabViewModel> Tabs => _tabs;

    public TerminalTabViewModel? ActiveTab
    {
        get => _activeTab;
        private set
        {
            this.RaiseAndSetIfChanged(ref _activeTab, value);
            this.RaisePropertyChanged(nameof(ActiveSession));
        }
    }

    public TerminalViewModel? ActiveSession => ActiveTab?.Session;

    public IObservable<string?> StartupError => _startupError;

    public ReactiveCommand<Unit, Unit> NewTabCommand { get; }
    public ReactiveCommand<TerminalTabViewModel, Unit> CloseTabCommand { get; }
    public ReactiveCommand<TerminalTabViewModel, Unit> ActivateTabCommand { get; }

    public TerminalHost(ITerminalSessionFactory factory)
    {
        _factory = factory;
        _tabs = new ObservableCollection<TerminalTabViewModel>();

        var initialSession = factory.CreateSession();
        var initialTab = new TerminalTabViewModel(initialSession) { IsActive = true };
        _tabs.Add(initialTab);
        ActiveTab = initialTab;

        _startupError = this.WhenAnyValue(h => h.ActiveTab)
            .Select(tab => tab != null
                ? tab.Session.WhenAnyValue(s => s.StartupError)
                : Observable.Return<string?>(null))
            .Switch();

        NewTabCommand = ReactiveCommand.Create(NewTab);
        CloseTabCommand = ReactiveCommand.Create<TerminalTabViewModel>(CloseTab);
        ActivateTabCommand = ReactiveCommand.Create<TerminalTabViewModel>(ActivateTab);
    }

    public async Task EnsureActiveSessionStartedAsync()
    {
        if (ActiveTab == null) return;
        await ActiveTab.Session.EnsureStartedAsync();
    }

    public void FocusActiveSession()
    {
    }

    public void NewTab()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(TerminalHost));

        var session = _factory.CreateSession();
        var tab = new TerminalTabViewModel(session);

        if (ActiveTab != null)
            ActiveTab.IsActive = false;

        tab.IsActive = true;
        _tabs.Add(tab);
        ActiveTab = tab;
    }

    public void CloseTab(TerminalTabViewModel tab)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(TerminalHost));
        if (tab == null) throw new ArgumentNullException(nameof(tab));
        if (!_tabs.Contains(tab)) return;

        var wasActive = tab == ActiveTab;
        int index = _tabs.IndexOf(tab);

        tab.IsActive = false;
        _tabs.Remove(tab);
        tab.Session.Dispose();

        if (_tabs.Count == 0)
        {
            ActiveTab = null;
            return;
        }

        if (wasActive)
        {
            int fallbackIndex = index < _tabs.Count ? index : _tabs.Count - 1;
            var fallback = _tabs[fallbackIndex];
            fallback.IsActive = true;
            ActiveTab = fallback;
        }
    }

    public void ActivateTab(TerminalTabViewModel tab)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(TerminalHost));
        if (tab == null) throw new ArgumentNullException(nameof(tab));
        if (!_tabs.Contains(tab)) return;
        if (tab == ActiveTab) return;

        if (ActiveTab != null)
            ActiveTab.IsActive = false;

        tab.IsActive = true;
        ActiveTab = tab;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var tab in _tabs)
            tab.Session.Dispose();

        _tabs.Clear();
        ActiveTab = null;
    }
}
