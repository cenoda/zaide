using Avalonia.Controls;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Zaide.Features.Editor.Presentation;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Presentation;

namespace Zaide.App.Shell;

/// <summary>
/// View-side settings overlay lifecycle: factory creation, layout attach/detach,
/// open/close interaction handling, left-panel mode restore, and editor focus
/// restoration after close.
/// </summary>
internal sealed class SettingsPanelAttachHost
{
    private readonly ISettingsService _settings;
    private readonly ISecretStore _secrets;
    private readonly ISettingsPanelFactory _settingsPanelFactory;
    private readonly Grid _layoutRoot;
    private readonly Func<EditorView> _getEditorView;

    private SettingsPanelView? _settingsPanel;
    private SettingsViewModel? _settingsPanelViewModel;
    private LeftPanelMode _settingsReturnLeftPanelMode = LeftPanelMode.Explorer;
    private MainWindowViewModel? _viewModel;

    public SettingsPanelAttachHost(
        ISettingsService settings,
        ISecretStore secrets,
        ISettingsPanelFactory settingsPanelFactory,
        Grid layoutRoot,
        Func<EditorView> getEditorView)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        _settingsPanelFactory = settingsPanelFactory
            ?? throw new ArgumentNullException(nameof(settingsPanelFactory));
        _layoutRoot = layoutRoot ?? throw new ArgumentNullException(nameof(layoutRoot));
        _getEditorView = getEditorView ?? throw new ArgumentNullException(nameof(getEditorView));
    }

    public void WireToViewModel(MainWindowViewModel viewModel, CompositeDisposable disposables)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(disposables);

        _viewModel = viewModel;
        disposables.Add(viewModel.ShowSettings.RegisterHandler(context =>
        {
            HandleShowSettings(context);
            return Task.CompletedTask;
        }));
        disposables.Add(Disposable.Create(ClosePanel));
    }

    internal SettingsPanelView? PanelForTests => _settingsPanel;

    internal void HandleShowSettings(IInteractionContext<System.Reactive.Unit, bool> context)
    {
        var vm = RequireViewModel();
        if (vm.IsSettingsOpen)
        {
            HidePanel();
            context.SetOutput(false);
            return;
        }

        ShowPanel();
        context.SetOutput(true);
    }

    internal void ShowPanel()
    {
        var vm = RequireViewModel();
        _settingsReturnLeftPanelMode = vm.LeftPanelMode;
        if (_settingsPanel is null)
        {
            var (viewModel, panel) =
                _settingsPanelFactory.Create(_settings, _secrets);
            _settingsPanelViewModel = viewModel;
            _settingsPanel = panel;
            Grid.SetColumn(panel, 0);
            Grid.SetColumnSpan(panel, 6);
            Grid.SetRow(panel, 0);
            Grid.SetRowSpan(panel, 3);
            viewModel.CloseRequested += OnSettingsCloseRequested;
        }

        AttachToLayout();
        vm.IsSettingsOpen = true;
    }

    internal void HidePanel()
    {
        var vm = RequireViewModel();
        if (_settingsPanel is null || !vm.IsSettingsOpen)
            return;

        DetachFromLayout();
        vm.IsSettingsOpen = false;
        vm.LeftPanelMode = _settingsReturnLeftPanelMode;
        RestoreEditorFocusAfterSettings(vm, _getEditorView());
    }

    internal void ClosePanel()
    {
        if (_settingsPanel is null)
            return;

        if (_settingsPanelViewModel is not null)
            _settingsPanelViewModel.CloseRequested -= OnSettingsCloseRequested;

        var panel = _settingsPanel;
        _settingsPanel = null;
        _settingsPanelViewModel = null;
        RequireViewModel().IsSettingsOpen = false;
        if (_layoutRoot.CheckAccess() && _layoutRoot.Children.Contains(panel))
            _layoutRoot.Children.Remove(panel);
        panel.Dispose();
    }

    internal static void RestoreEditorFocusAfterSettings(
        MainWindowViewModel? viewModel,
        EditorView? editorView)
    {
        var activeTab = viewModel?.EditorTabs.ActiveTab;
        if (activeTab is not null && editorView is not null && editorView.IsVisible)
            editorView.Focus();
    }

    private void OnSettingsCloseRequested(object? sender, EventArgs e) => HidePanel();

    private void AttachToLayout()
    {
        if (_settingsPanel is null || !_layoutRoot.CheckAccess())
            return;

        if (!_layoutRoot.Children.Contains(_settingsPanel))
            _layoutRoot.Children.Add(_settingsPanel);
    }

    private void DetachFromLayout()
    {
        if (_settingsPanel is null || !_layoutRoot.CheckAccess())
            return;

        if (_layoutRoot.Children.Contains(_settingsPanel))
            _layoutRoot.Children.Remove(_settingsPanel);
    }

    private MainWindowViewModel RequireViewModel() =>
        _viewModel
        ?? throw new InvalidOperationException("Settings panel attach host is not bound to a ViewModel.");
}
