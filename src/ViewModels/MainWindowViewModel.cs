using System;
using ReactiveUI;
using System.Reactive;
using System.Reactive.Linq;

namespace Zaide.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    private bool _isBottomPanelVisible;
    private string? _statusText = "Open a folder to begin";

    public bool IsBottomPanelVisible
    {
        get => _isBottomPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isBottomPanelVisible, value);
    }

    public string? StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public ReactiveCommand<Unit, Unit> ToggleBottomPanelCommand { get; }

    public FileTreeViewModel FileTreeViewModel { get; }

    public MainWindowViewModel(FileTreeViewModel fileTreeViewModel)
    {
        FileTreeViewModel = fileTreeViewModel;

        ToggleBottomPanelCommand = ReactiveCommand.Create(ToggleBottomPanel);

        this.WhenAnyValue(x => x.FileTreeViewModel.SelectedFile)
            .Subscribe(file => StatusText = file is not null
                ? $"Opened: {file.Name}"
                : "No file selected");
    }

    private void ToggleBottomPanel()
    {
        IsBottomPanelVisible = !IsBottomPanelVisible;
    }
}
