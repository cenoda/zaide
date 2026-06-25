using ReactiveUI;
using System.Reactive;

namespace Zaide.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    private bool _isBottomPanelVisible;

    public bool IsBottomPanelVisible
    {
        get => _isBottomPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isBottomPanelVisible, value);
    }

    public ReactiveCommand<Unit, Unit> ToggleBottomPanelCommand { get; }

    public FileTreeViewModel FileTreeViewModel { get; }

    public MainWindowViewModel(FileTreeViewModel fileTreeViewModel)
    {
        FileTreeViewModel = fileTreeViewModel;

        ToggleBottomPanelCommand = ReactiveCommand.Create(ToggleBottomPanel);
    }

    private void ToggleBottomPanel()
    {
        IsBottomPanelVisible = !IsBottomPanelVisible;
    }
}
