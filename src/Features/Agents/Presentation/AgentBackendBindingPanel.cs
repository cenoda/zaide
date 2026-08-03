using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using Zaide.UI.DesignSystem;

namespace Zaide.Features.Agents.Presentation;

/// <summary>
/// Interactive Townhall backend binding panel for direct agent conversations.
/// Keyboard-focusable bind/unbind controls with automation names.
/// </summary>
public sealed class AgentBackendBindingPanel : Panel
{
    private readonly TextBlock _backendLabel;
    private readonly TextBlock _authStatusCaption;
    private readonly TextBlock _capabilityCaption;
    private readonly TextBlock _settingsCaption;
    private readonly TextBlock _mutationErrorCaption;
    private readonly TextBlock _acpRuntimeCaption;
    private readonly TextBox _acpExecutableInput;
    private readonly TextBox _acpArgumentsInput;
    private readonly TextBox _acpExpectedNameInput;
    private readonly TextBox _acpExpectedVersionInput;
    private readonly Button _bindNativeButton;
    private readonly Button _bindAcpButton;
    private readonly Button _unbindButton;
    private readonly Button _endSessionButton;
    private readonly Button _probeAcpButton;
    private readonly Button _authenticateAcpButton;
    private readonly Button _logoutButton;
    private readonly StackPanel _actionsRow;
    private readonly StackPanel _acpRow;
    private readonly StackPanel _acpConfigRow;

    public event EventHandler? BindNativeHarnessRequested;

    public event EventHandler? BindAcpRequested;

    public event EventHandler? UnbindRequested;

    public event EventHandler? EndSessionRequested;

    public event EventHandler? ProbeAcpRequested;

    public event EventHandler? AuthenticateAcpRequested;

    public event EventHandler? LogoutRequested;

    public AgentBackendBindingPanel()
    {
        _backendLabel = TextStyles.Caption("Unbound");
        _backendLabel.VerticalAlignment = VerticalAlignment.Center;
        AutomationProperties.SetName(_backendLabel, "Agent backend binding label");

        _authStatusCaption = TextStyles.Caption(string.Empty);
        _authStatusCaption.Foreground = Brushes.Gray;
        _authStatusCaption.VerticalAlignment = VerticalAlignment.Center;
        AutomationProperties.SetName(_authStatusCaption, "Agent backend authentication status");

        _capabilityCaption = TextStyles.Caption(string.Empty);
        _capabilityCaption.Foreground = Brushes.Gray;
        _capabilityCaption.VerticalAlignment = VerticalAlignment.Center;
        AutomationProperties.SetName(_capabilityCaption, "Agent backend capability status");

        _settingsCaption = TextStyles.Caption(string.Empty);
        _settingsCaption.Foreground = Brushes.Gray;
        _settingsCaption.VerticalAlignment = VerticalAlignment.Center;
        AutomationProperties.SetName(_settingsCaption, "Agent backend settings guidance");

        _mutationErrorCaption = TextStyles.Caption(string.Empty);
        _mutationErrorCaption.Foreground = Brushes.IndianRed;
        _mutationErrorCaption.VerticalAlignment = VerticalAlignment.Center;
        _mutationErrorCaption.IsVisible = false;
        AutomationProperties.SetName(_mutationErrorCaption, "Agent backend binding error");

        _acpRuntimeCaption = TextStyles.Caption(string.Empty);
        _acpRuntimeCaption.Foreground = Brushes.Gray;
        _acpRuntimeCaption.VerticalAlignment = VerticalAlignment.Center;
        _acpRuntimeCaption.IsVisible = false;
        AutomationProperties.SetName(_acpRuntimeCaption, "ACP runtime identity");

        _acpExecutableInput = CreateConfigInput("ACP executable path");
        _acpArgumentsInput = CreateConfigInput("ACP non-secret arguments");
        _acpExpectedNameInput = CreateConfigInput("ACP expected agent name");
        _acpExpectedVersionInput = CreateConfigInput("ACP expected agent version");

        _bindNativeButton = CreateActionButton(
            "Bind Native Harness",
            "Bind Native Harness backend");
        _bindNativeButton.Click += (_, _) => BindNativeHarnessRequested?.Invoke(this, EventArgs.Empty);

        _bindAcpButton = CreateActionButton(
            "Bind ACP",
            "Bind ACP backend");
        _bindAcpButton.Click += (_, _) => BindAcpRequested?.Invoke(this, EventArgs.Empty);

        _unbindButton = CreateActionButton(
            "Unbind",
            "Unbind agent backend");
        _unbindButton.Click += (_, _) => UnbindRequested?.Invoke(this, EventArgs.Empty);

        _endSessionButton = CreateActionButton(
            "End session",
            "End agent session");
        _endSessionButton.Click += (_, _) => EndSessionRequested?.Invoke(this, EventArgs.Empty);

        _probeAcpButton = CreateActionButton(
            "Probe ACP",
            "Probe ACP runtime configuration");
        _probeAcpButton.Click += (_, _) => ProbeAcpRequested?.Invoke(this, EventArgs.Empty);
        _probeAcpButton.IsVisible = false;

        _authenticateAcpButton = CreateActionButton(
            "Authenticate ACP",
            "Authenticate ACP with advertised method");
        _authenticateAcpButton.Click += (_, _) => AuthenticateAcpRequested?.Invoke(this, EventArgs.Empty);
        _authenticateAcpButton.IsVisible = false;

        _logoutButton = CreateActionButton(
            "Logout ACP",
            "Logout ACP authentication");
        _logoutButton.Click += (_, _) => LogoutRequested?.Invoke(this, EventArgs.Empty);
        _logoutButton.IsVisible = false;

        _actionsRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingSm,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                _bindNativeButton,
                _bindAcpButton,
                _unbindButton,
                _endSessionButton,
                _probeAcpButton,
                _authenticateAcpButton,
                _logoutButton,
            },
        };

        _acpConfigRow = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = LayoutTokens.SpacingXs,
            Children =
            {
                _acpExecutableInput,
                _acpArgumentsInput,
                _acpExpectedNameInput,
                _acpExpectedVersionInput,
            },
        };

        _acpRow = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = LayoutTokens.SpacingXs,
            Children = { _acpRuntimeCaption, _acpConfigRow },
        };

        var statusRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingSm,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                TextStyles.Caption("Backend:"),
                _backendLabel,
                _authStatusCaption,
            },
        };

        var body = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = LayoutTokens.SpacingXs,
            Children =
            {
                statusRow,
                _capabilityCaption,
                _settingsCaption,
                _acpRow,
                _actionsRow,
                _mutationErrorCaption,
            },
        };

        var container = new Border
        {
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingMd, LayoutTokens.SpacingXs),
            Child = body,
        };

        AutomationProperties.SetName(container, "Agent backend binding panel");
        Focusable = true;
        Children.Add(container);
    }

    public bool IsPanelVisible
    {
        get => IsVisible;
        set => IsVisible = value;
    }

    /// <summary>
    /// Compatibility projection used by status-only callers.
    /// </summary>
    public void SetBindingProjection(
        string backendLabel,
        string authStatusCaption,
        bool isDisconnected)
    {
        SetWorkflowProjection(
            backendLabel,
            authStatusCaption,
            isDisconnected,
            capabilityCaption: string.Empty,
            settingsCaption: string.Empty,
            mutationErrorCaption: null,
            canBindNativeHarness: false,
            canUnbind: false,
            canEndSession: false,
            acpRuntimeCaption: null,
            canProbeAcp: false,
            canAuthenticateAcp: false,
            canLogout: false,
            canBindAcp: false,
            showAcpConfig: false);
    }

    public void SetWorkflowProjection(
        string backendLabel,
        string authStatusCaption,
        bool isDisconnected,
        string capabilityCaption,
        string settingsCaption,
        string? mutationErrorCaption,
        bool canBindNativeHarness,
        bool canUnbind,
        bool canEndSession = false,
        string? acpRuntimeCaption = null,
        bool canProbeAcp = false,
        bool canAuthenticateAcp = false,
        bool canLogout = false,
        bool canBindAcp = true,
        bool showAcpConfig = true)
    {
        _backendLabel.Text = backendLabel;
        _authStatusCaption.Text = authStatusCaption;
        _authStatusCaption.IsVisible = !string.IsNullOrEmpty(authStatusCaption);
        _authStatusCaption.Foreground = isDisconnected ? Brushes.IndianRed : Brushes.Gray;

        _capabilityCaption.Text = capabilityCaption;
        _capabilityCaption.IsVisible = !string.IsNullOrEmpty(capabilityCaption);

        _settingsCaption.Text = settingsCaption;
        _settingsCaption.IsVisible = !string.IsNullOrEmpty(settingsCaption);

        _mutationErrorCaption.Text = mutationErrorCaption ?? string.Empty;
        _mutationErrorCaption.IsVisible = !string.IsNullOrEmpty(mutationErrorCaption);

        _bindNativeButton.IsEnabled = canBindNativeHarness;
        _bindNativeButton.IsVisible = true;
        _bindAcpButton.IsEnabled = canBindAcp;
        _bindAcpButton.IsVisible = true;
        _unbindButton.IsEnabled = canUnbind;
        _unbindButton.IsVisible = true;
        _endSessionButton.IsEnabled = canEndSession;
        _endSessionButton.IsVisible = canEndSession;

        _acpRuntimeCaption.Text = acpRuntimeCaption ?? string.Empty;
        _acpRuntimeCaption.IsVisible = !string.IsNullOrEmpty(acpRuntimeCaption);
        _acpConfigRow.IsVisible = showAcpConfig;
        _probeAcpButton.IsVisible = canProbeAcp;
        _probeAcpButton.IsEnabled = canProbeAcp;
        _authenticateAcpButton.IsVisible = canAuthenticateAcp;
        _authenticateAcpButton.IsEnabled = canAuthenticateAcp;
        _logoutButton.IsVisible = canLogout;
        _logoutButton.IsEnabled = canLogout;
    }

    /// <summary>
    /// Test/automation hook for ACP config-row visibility.
    /// </summary>
    public bool IsAcpConfigRowVisible => _acpConfigRow.IsVisible;

    public string AcpExecutablePath
    {
        get => _acpExecutableInput.Text ?? string.Empty;
        set => _acpExecutableInput.Text = value;
    }

    public string AcpArgumentsText
    {
        get => _acpArgumentsInput.Text ?? string.Empty;
        set => _acpArgumentsInput.Text = value;
    }

    public string AcpExpectedAgentName
    {
        get => _acpExpectedNameInput.Text ?? string.Empty;
        set => _acpExpectedNameInput.Text = value;
    }

    public string AcpExpectedAgentVersion
    {
        get => _acpExpectedVersionInput.Text ?? string.Empty;
        set => _acpExpectedVersionInput.Text = value;
    }

    /// <summary>
    /// Test/automation hooks for focusable action controls.
    /// </summary>
    public Button BindNativeHarnessButton => _bindNativeButton;

    public Button BindAcpButton => _bindAcpButton;

    public Button UnbindButton => _unbindButton;

    public Button EndSessionButton => _endSessionButton;

    public Button ProbeAcpButton => _probeAcpButton;

    public Button AuthenticateAcpButton => _authenticateAcpButton;

    public Button LogoutButton => _logoutButton;

    private static Button CreateActionButton(string content, string automationName)
    {
        var button = new Button
        {
            Content = TextStyles.Caption(content),
            VerticalAlignment = VerticalAlignment.Center,
            Focusable = true,
            IsTabStop = true,
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingSm, LayoutTokens.SpacingXs),
        };
        AutomationProperties.SetName(button, automationName);
        return button;
    }

    private static TextBox CreateConfigInput(string automationName)
    {
        var input = new TextBox
        {
            PlaceholderText = automationName,
            MinWidth = 220,
            Focusable = true,
            IsTabStop = true,
        };
        AutomationProperties.SetName(input, automationName);
        return input;
    }
}
