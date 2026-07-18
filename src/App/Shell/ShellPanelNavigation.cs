using System;
using System.Reactive;
using ReactiveUI;

namespace Zaide.App.Shell;

/// <summary>
/// Owns panel-navigation command construction and decision actions only.
/// Mode storage and <c>RaiseAndSetIfChanged</c> remain on
/// <see cref="MainWindowViewModel"/>; mutations go through injected delegates.
/// Constructed inside <see cref="MainWindowViewModel"/>; not DI-registered.
/// </summary>
internal sealed class ShellPanelNavigation
{
    private readonly Action<LeftPanelMode> _setLeft;
    private readonly Action<BottomPanelMode> _setBottom;
    private readonly Action<bool> _setBottomVisible;
    private readonly Func<bool> _getBottomVisible;

    public ShellPanelNavigation(
        Action<LeftPanelMode> setLeft,
        Action<BottomPanelMode> setBottom,
        Action<bool> setBottomVisible,
        Func<bool> getBottomVisible)
    {
        _setLeft = setLeft ?? throw new ArgumentNullException(nameof(setLeft));
        _setBottom = setBottom ?? throw new ArgumentNullException(nameof(setBottom));
        _setBottomVisible = setBottomVisible ?? throw new ArgumentNullException(nameof(setBottomVisible));
        _getBottomVisible = getBottomVisible ?? throw new ArgumentNullException(nameof(getBottomVisible));

        SwitchToExplorerCommand = ReactiveCommand.Create(
            () => _setLeft(LeftPanelMode.Explorer));
        SwitchToSourceControlCommand = ReactiveCommand.Create(
            () => _setLeft(LeftPanelMode.SourceControl));
        SwitchToTerminalBottomCommand = ReactiveCommand.Create(() =>
        {
            _setBottom(BottomPanelMode.Terminal);
            _setBottomVisible(true);
        });
        SwitchToProblemsBottomCommand = ReactiveCommand.Create(() =>
        {
            _setBottom(BottomPanelMode.Problems);
            _setBottomVisible(true);
        });
        SwitchToOutputBottomCommand = ReactiveCommand.Create(() =>
        {
            _setBottom(BottomPanelMode.Output);
            _setBottomVisible(true);
        });
        SwitchToTestResultsBottomCommand = ReactiveCommand.Create(() =>
        {
            _setBottom(BottomPanelMode.TestResults);
            _setBottomVisible(true);
        });
        SwitchToDebugBottomCommand = ReactiveCommand.Create(() =>
        {
            _setBottom(BottomPanelMode.Debug);
            _setBottomVisible(true);
        });
        ToggleBottomPanelCommand = ReactiveCommand.Create(
            () => _setBottomVisible(!_getBottomVisible()));
        HideBottomPanelCommand = ReactiveCommand.Create(
            () => _setBottomVisible(false));
    }

    public ReactiveCommand<Unit, Unit> SwitchToExplorerCommand { get; }
    public ReactiveCommand<Unit, Unit> SwitchToSourceControlCommand { get; }
    public ReactiveCommand<Unit, Unit> SwitchToTerminalBottomCommand { get; }
    public ReactiveCommand<Unit, Unit> SwitchToProblemsBottomCommand { get; }
    public ReactiveCommand<Unit, Unit> SwitchToOutputBottomCommand { get; }
    public ReactiveCommand<Unit, Unit> SwitchToTestResultsBottomCommand { get; }
    public ReactiveCommand<Unit, Unit> SwitchToDebugBottomCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleBottomPanelCommand { get; }
    public ReactiveCommand<Unit, Unit> HideBottomPanelCommand { get; }
}
