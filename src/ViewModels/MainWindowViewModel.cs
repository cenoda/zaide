using ReactiveUI;

namespace Zaide.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    private bool _isBottomPanelVisible;

    public bool IsBottomPanelVisible
    {
        get => _isBottomPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isBottomPanelVisible, value);
    }
}
