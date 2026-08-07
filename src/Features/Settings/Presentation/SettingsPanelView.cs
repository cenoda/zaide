using System;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ReactiveUI.Avalonia;
using Zaide.UI.DesignSystem;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;

namespace Zaide.Features.Settings.Presentation;

/// <summary>
/// Full-content slide-over surface for transient settings editing.
/// Sections labelled Editor, Terminal, LLM, and Agents.
/// </summary>
public sealed class SettingsPanelView : ReactiveUserControl<SettingsViewModel>, IDisposable
{
    private readonly SettingsViewModel _settingsViewModel;
    private readonly TextBlock _errors;
    private readonly TextBlock _conflict;

    // Editor controls
    private readonly SettingsFontPicker _codeFontFamily;
    private readonly TextBox _codeFontSize;
    private readonly SettingsFontPicker _proseFontFamily;
    private readonly TextBox _tabSize;
    private readonly CheckBox _insertSpaces;
    private readonly CheckBox _showWhitespace;
    private readonly CheckBox _showTabs;
    private readonly CheckBox _showSpaces;
    private readonly CheckBox _formatOnSave;

    // Terminal controls
    private readonly SettingsFontPicker _terminalFontFamily;
    private readonly TextBox _terminalFontSize;

    // LLM controls
    private readonly TextBox _model;
    private readonly TextBox _baseUrl;
    private readonly TextBox _apiKey;

    // Agents controls
    private readonly CheckBox _traceCaptureEnabled;
    private readonly CheckBox _usageCaptureEnabled;
    private readonly TextBox _tracePageSize;
    private readonly TextBox _traceMaxPageSize;
    private readonly TextBox _acpExecutablePath;
    private readonly TextBox _acpArguments;
    private readonly TextBox _acpExpectedAgentName;
    private readonly TextBox _acpExpectedAgentVersion;
    private readonly ComboBox _defaultContextPolicy;

    private bool _syncing;
    private bool _disposed;

    public SettingsPanelView(SettingsViewModel viewModel)
    {
        _settingsViewModel = viewModel;
        ViewModel = viewModel;
        ThemeBinding.SetBrush(this, BackgroundProperty, "SurfacePanelBrush");
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;

        // ── Editor controls ───────────────────────────────────────────
        _codeFontFamily = new SettingsFontPicker(viewModel.SetCodeFontFamily);
        _codeFontFamily.SetSelectedFamily(viewModel.Candidate.Editor.CodeFontFamily);
        _codeFontSize = new TextBox
        {
            Text = viewModel.Candidate.Editor.CodeFontSize.ToString(),
            PlaceholderText = "14"
        };
        _proseFontFamily = new SettingsFontPicker(viewModel.SetProseFontFamily);
        _proseFontFamily.SetSelectedFamily(viewModel.Candidate.Editor.ProseFontFamily);
        _tabSize = new TextBox
        {
            Text = viewModel.Candidate.Editor.TabSize.ToString(),
            PlaceholderText = "4"
        };
        _insertSpaces = new CheckBox
        {
            IsChecked = viewModel.Candidate.Editor.InsertSpaces,
            Content = "Insert Spaces"
        };
        _showWhitespace = new CheckBox
        {
            IsChecked = viewModel.Candidate.Editor.ShowWhitespace,
            Content = "Show Whitespace"
        };
        _showTabs = new CheckBox
        {
            IsChecked = viewModel.Candidate.Editor.ShowTabs,
            Content = "Show Tabs"
        };
        _showSpaces = new CheckBox
        {
            IsChecked = viewModel.Candidate.Editor.ShowSpaces,
            Content = "Show Spaces"
        };
        _formatOnSave = new CheckBox
        {
            IsChecked = viewModel.Candidate.Editor.FormatOnSave,
            Content = "Format on Save"
        };

        // ── Terminal controls ──────────────────────────────────────────
        _terminalFontFamily = new SettingsFontPicker(viewModel.SetTerminalFontFamily);
        _terminalFontFamily.SetSelectedFamily(viewModel.Candidate.Editor.TerminalFontFamily);
        _terminalFontSize = new TextBox
        {
            Text = viewModel.Candidate.Editor.TerminalFontSize.ToString(),
            PlaceholderText = "14"
        };

        // ── LLM controls ───────────────────────────────────────────────
        _model = new TextBox { Text = viewModel.Candidate.Llm.Model, PlaceholderText = "Model" };
        _baseUrl = new TextBox { Text = viewModel.Candidate.Llm.BaseUrl, PlaceholderText = "Base URL" };
        _apiKey = new TextBox { Text = viewModel.ApiKey ?? "", PasswordChar = '•', PlaceholderText = "API key" };

        // ── Agents controls ────────────────────────────────────────────
        _traceCaptureEnabled = new CheckBox
        {
            IsChecked = viewModel.Candidate.Agents.TraceCaptureEnabled,
            Content = "Enable trace capture by default"
        };
        AutomationProperties.SetName(_traceCaptureEnabled, "Enable trace capture by default");

        _usageCaptureEnabled = new CheckBox
        {
            IsChecked = viewModel.Candidate.Agents.UsageCaptureEnabled,
            Content = "Enable usage capture by default"
        };
        AutomationProperties.SetName(_usageCaptureEnabled, "Enable usage capture by default");

        _tracePageSize = new TextBox
        {
            Text = viewModel.Candidate.Agents.TracePageSize.ToString(),
            PlaceholderText = "64"
        };
        AutomationProperties.SetName(_tracePageSize, "Trace page size");

        _traceMaxPageSize = new TextBox
        {
            Text = viewModel.Candidate.Agents.TraceMaxPageSize.ToString(),
            PlaceholderText = "256"
        };
        AutomationProperties.SetName(_traceMaxPageSize, "Trace max page size");

        _acpExecutablePath = new TextBox
        {
            Text = viewModel.Candidate.Agents.AcpExecutablePath,
            PlaceholderText = "ACP executable path"
        };
        AutomationProperties.SetName(_acpExecutablePath, "ACP executable path");

        _acpArguments = new TextBox
        {
            Text = viewModel.Candidate.Agents.AcpArguments,
            PlaceholderText = "ACP non-secret arguments"
        };
        AutomationProperties.SetName(_acpArguments, "ACP non-secret arguments");

        _acpExpectedAgentName = new TextBox
        {
            Text = viewModel.Candidate.Agents.AcpExpectedAgentName,
            PlaceholderText = "ACP expected agent name"
        };
        AutomationProperties.SetName(_acpExpectedAgentName, "ACP expected agent name");

        _acpExpectedAgentVersion = new TextBox
        {
            Text = viewModel.Candidate.Agents.AcpExpectedAgentVersion,
            PlaceholderText = "ACP expected agent version"
        };
        AutomationProperties.SetName(_acpExpectedAgentVersion, "ACP expected agent version");

        _defaultContextPolicy = new ComboBox
        {
            MinWidth = 200,
            ItemsSource = new[] { "Off", "Minimal", "Standard", "Detailed" },
            SelectedItem = viewModel.Candidate.Agents.DefaultContextPolicyLevel,
        };
        AutomationProperties.SetName(_defaultContextPolicy, "Default context policy level");
        AutomationProperties.SetHelpText(
            _defaultContextPolicy,
            "Application-wide default IDE context disclosure policy for agent sessions.");

        // ── Status displays ────────────────────────────────────────────
        _errors = TextStyles.Caption("");
        _errors.Foreground = ThemeBinding.GetBrush("DangerBrush");
        _conflict = TextStyles.Caption("");
        _conflict.Foreground = ThemeBinding.GetBrush("WarningBrush");

        // ── Button bar ─────────────────────────────────────────────────
        var apply = new Button { Content = "Apply", Command = viewModel.ApplyCommand };
        var rebase = new Button { Content = "Rebase / Refresh", Command = viewModel.RebaseCommand };
        var discard = new Button { Content = "Discard", Command = viewModel.DiscardCommand };
        var close = new Button { Content = "Close", Command = viewModel.CloseCommand };

        // ── Wire events ─────────────────────────────────────────────────
        // Editor text fields
        _codeFontSize.TextChanged += (_, _) => { if (!_syncing && int.TryParse(_codeFontSize.Text, out var s)) viewModel.SetCodeFontSize(s); };
        _tabSize.TextChanged += (_, _) => { if (!_syncing && int.TryParse(_tabSize.Text, out var s)) viewModel.SetTabSize(s); };
        // Editor checkbox fields
        _insertSpaces.PropertyChanged += (_, e) =>
        {
            if (e.Property == CheckBox.IsCheckedProperty && !_syncing)
                viewModel.SetInsertSpaces(_insertSpaces.IsChecked ?? true);
        };
        _showWhitespace.PropertyChanged += (_, e) =>
        {
            if (e.Property == CheckBox.IsCheckedProperty && !_syncing)
                viewModel.SetShowWhitespace(_showWhitespace.IsChecked ?? false);
        };
        _showTabs.PropertyChanged += (_, e) =>
        {
            if (e.Property == CheckBox.IsCheckedProperty && !_syncing)
                viewModel.SetShowTabs(_showTabs.IsChecked ?? false);
        };
        _showSpaces.PropertyChanged += (_, e) =>
        {
            if (e.Property == CheckBox.IsCheckedProperty && !_syncing)
                viewModel.SetShowSpaces(_showSpaces.IsChecked ?? false);
        };
        _formatOnSave.PropertyChanged += (_, e) =>
        {
            if (e.Property == CheckBox.IsCheckedProperty && !_syncing)
                viewModel.SetFormatOnSave(_formatOnSave.IsChecked ?? false);
        };
        // Terminal text fields
        _terminalFontSize.TextChanged += (_, _) => { if (!_syncing && int.TryParse(_terminalFontSize.Text, out var s)) viewModel.SetTerminalFontSize(s); };
        // LLM text fields
        _model.TextChanged += (_, _) => { if (!_syncing) viewModel.SetModel(_model.Text ?? ""); };
        _baseUrl.TextChanged += (_, _) => { if (!_syncing) viewModel.SetBaseUrl(_baseUrl.Text ?? ""); };
        _apiKey.TextChanged += (_, _) => { if (!_syncing) viewModel.ApiKey = _apiKey.Text; };
        _traceCaptureEnabled.PropertyChanged += (_, e) =>
        {
            if (e.Property == CheckBox.IsCheckedProperty && !_syncing)
                viewModel.SetTraceCaptureEnabled(_traceCaptureEnabled.IsChecked ?? false);
        };
        _usageCaptureEnabled.PropertyChanged += (_, e) =>
        {
            if (e.Property == CheckBox.IsCheckedProperty && !_syncing)
                viewModel.SetUsageCaptureEnabled(_usageCaptureEnabled.IsChecked ?? false);
        };
        _tracePageSize.TextChanged += (_, _) =>
        {
            if (!_syncing && int.TryParse(_tracePageSize.Text, out var size))
                viewModel.SetTracePageSize(size);
        };
        _traceMaxPageSize.TextChanged += (_, _) =>
        {
            if (!_syncing && int.TryParse(_traceMaxPageSize.Text, out var size))
                viewModel.SetTraceMaxPageSize(size);
        };
        _acpExecutablePath.TextChanged += (_, _) =>
        {
            if (!_syncing) viewModel.SetAcpExecutablePath(_acpExecutablePath.Text ?? string.Empty);
        };
        _acpArguments.TextChanged += (_, _) =>
        {
            if (!_syncing) viewModel.SetAcpArguments(_acpArguments.Text ?? string.Empty);
        };
        _acpExpectedAgentName.TextChanged += (_, _) =>
        {
            if (!_syncing) viewModel.SetAcpExpectedAgentName(_acpExpectedAgentName.Text ?? string.Empty);
        };
        _acpExpectedAgentVersion.TextChanged += (_, _) =>
        {
            if (!_syncing) viewModel.SetAcpExpectedAgentVersion(_acpExpectedAgentVersion.Text ?? string.Empty);
        };
        _defaultContextPolicy.SelectionChanged += (_, _) =>
        {
            if (!_syncing && _defaultContextPolicy.SelectedItem is string level)
                viewModel.SetDefaultContextPolicyLevel(level);
        };

        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // ── Build layout ────────────────────────────────────────────────
        var editorSection = BuildSection("Editor",
            LabelledField("Code Font Family", _codeFontFamily),
            LabelledField("Code Font Size", _codeFontSize),
            LabelledField("Prose Font Family", _proseFontFamily),
            LabelledField("Tab Size", _tabSize),
            _insertSpaces, _showWhitespace, _showTabs, _showSpaces, _formatOnSave);

        var terminalSection = BuildSection("Terminal",
            LabelledField("Terminal Font Family", _terminalFontFamily),
            LabelledField("Terminal Font Size", _terminalFontSize));

        var llmSection = BuildSection("LLM",
            LabelledField("Model", _model),
            LabelledField("Base URL", _baseUrl),
            LabelledField("API Key", _apiKey));

        var agentsSection = BuildSection("Agents",
            _traceCaptureEnabled,
            _usageCaptureEnabled,
            LabelledField("Trace page size", _tracePageSize),
            LabelledField("Trace max page size", _traceMaxPageSize),
            LabelledField("ACP executable path", _acpExecutablePath),
            LabelledField("ACP arguments (non-secret)", _acpArguments),
            LabelledField("ACP expected agent name", _acpExpectedAgentName),
            LabelledField("ACP expected agent version", _acpExpectedAgentVersion),
            LabelledField("Default context policy", _defaultContextPolicy));

        // ScrollViewer so every setting stays reachable when content exceeds
        // the available height (mouse wheel / trackpad / scrollbar).
        Content = new Border
        {
            Padding = LayoutTokens.Inset(LayoutTokens.SpacingXl, LayoutTokens.SpacingXl, LayoutTokens.SpacingXl, LayoutTokens.SpacingXl),
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = new StackPanel
                {
                    Width = 520,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Spacing = LayoutTokens.SpacingMd,
                    Children =
                    {
                        TextStyles.Header("Settings"),
                        TextStyles.Caption("Saved settings. Environment variables may override runtime values."),
                        editorSection,
                        terminalSection,
                        llmSection,
                        agentsSection,
                        _conflict, _errors,
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = LayoutTokens.SpacingSm,
                            Children = { apply, rebase, discard, close }
                        }
                    }
                }
            }
        };
        UpdateErrors();
        UpdateConflict();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.ValidationErrors)) UpdateErrors();
        if (e.PropertyName == nameof(SettingsViewModel.ConflictSnapshot)) UpdateConflict();
        if (e.PropertyName == nameof(SettingsViewModel.Candidate)) SyncFields();
    }

    private void UpdateErrors()
    {
        _errors.Text = string.Join(Environment.NewLine,
            ViewModel?.ValidationErrors.Select(error => $"{error.PropertyPath}: {error.Message}") ?? Array.Empty<string>());
    }

    private void UpdateConflict()
    {
        var conflict = ViewModel?.ConflictSnapshot;
        _conflict.Text = conflict is null
            ? ""
            : "Settings changed outside this panel. Rebase / Refresh to keep your edits and retry Apply.";
        _conflict.IsVisible = conflict is not null;
    }

    private void SyncFields()
    {
        if (ViewModel is null || _syncing) return;
        _syncing = true;
        _codeFontFamily.SetSelectedFamily(ViewModel.Candidate.Editor.CodeFontFamily);
        _codeFontSize.Text = ViewModel.Candidate.Editor.CodeFontSize.ToString();
        _proseFontFamily.SetSelectedFamily(ViewModel.Candidate.Editor.ProseFontFamily);
        _tabSize.Text = ViewModel.Candidate.Editor.TabSize.ToString();
        _insertSpaces.IsChecked = ViewModel.Candidate.Editor.InsertSpaces;
        _showWhitespace.IsChecked = ViewModel.Candidate.Editor.ShowWhitespace;
        _showTabs.IsChecked = ViewModel.Candidate.Editor.ShowTabs;
        _showSpaces.IsChecked = ViewModel.Candidate.Editor.ShowSpaces;
        _formatOnSave.IsChecked = ViewModel.Candidate.Editor.FormatOnSave;
        _terminalFontFamily.SetSelectedFamily(ViewModel.Candidate.Editor.TerminalFontFamily);
        _terminalFontSize.Text = ViewModel.Candidate.Editor.TerminalFontSize.ToString();
        _model.Text = ViewModel.Candidate.Llm.Model;
        _baseUrl.Text = ViewModel.Candidate.Llm.BaseUrl;
        _traceCaptureEnabled.IsChecked = ViewModel.Candidate.Agents.TraceCaptureEnabled;
        _usageCaptureEnabled.IsChecked = ViewModel.Candidate.Agents.UsageCaptureEnabled;
        _tracePageSize.Text = ViewModel.Candidate.Agents.TracePageSize.ToString();
        _traceMaxPageSize.Text = ViewModel.Candidate.Agents.TraceMaxPageSize.ToString();
        _acpExecutablePath.Text = ViewModel.Candidate.Agents.AcpExecutablePath;
        _acpArguments.Text = ViewModel.Candidate.Agents.AcpArguments;
        _acpExpectedAgentName.Text = ViewModel.Candidate.Agents.AcpExpectedAgentName;
        _acpExpectedAgentVersion.Text = ViewModel.Candidate.Agents.AcpExpectedAgentVersion;
        _defaultContextPolicy.SelectedItem = ViewModel.Candidate.Agents.DefaultContextPolicyLevel;
        _syncing = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _settingsViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _settingsViewModel.Dispose();
    }

    /// <summary>Builds a labelled section with a header and child controls.</summary>
    private static StackPanel BuildSection(string title, params Control[] children)
    {
        var panel = new StackPanel
        {
            Spacing = LayoutTokens.SpacingSm
        };
        // Section header
        var sectionHeader = new TextBlock
        {
            Text = title,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, LayoutTokens.SpacingSm, 0, LayoutTokens.SpacingXxs)
        };
        ThemeBinding.SetBrush(sectionHeader, TextBlock.ForegroundProperty, "TextPrimaryBrush");
        panel.Children.Add(sectionHeader);
        foreach (var child in children)
            panel.Children.Add(child);
        return panel;
    }

    /// <summary>Builds a labelled field with a caption above the control.</summary>
    private static StackPanel LabelledField(string label, Control control)
    {
        return new StackPanel
        {
            Spacing = LayoutTokens.SpacingXxs,
            Children =
            {
                TextStyles.Caption(label),
                control
            }
        };
    }
}
