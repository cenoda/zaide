using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ReactiveUI.Avalonia;
using Zaide.Styles;
using Zaide.ViewModels;

namespace Zaide.Views;

/// <summary>
/// Full-content slide-over surface for transient settings editing.
/// Sections labelled Editor, Terminal, and LLM.
/// </summary>
public sealed class SettingsPanelView : ReactiveUserControl<SettingsViewModel>, IDisposable
{
    private readonly SettingsViewModel _settingsViewModel;
    private readonly TextBlock _errors;
    private readonly TextBlock _conflict;

    // Editor controls
    private readonly TextBox _codeFontFamily;
    private readonly TextBox _codeFontSize;
    private readonly TextBox _proseFontFamily;
    private readonly TextBox _tabSize;
    private readonly CheckBox _insertSpaces;
    private readonly CheckBox _showWhitespace;
    private readonly CheckBox _showTabs;
    private readonly CheckBox _showSpaces;

    // Terminal controls
    private readonly TextBox _terminalFontFamily;
    private readonly TextBox _terminalFontSize;

    // LLM controls
    private readonly TextBox _model;
    private readonly TextBox _baseUrl;
    private readonly TextBox _apiKey;

    private bool _syncing;
    private bool _disposed;

    public SettingsPanelView(SettingsViewModel viewModel)
    {
        _settingsViewModel = viewModel;
        ViewModel = viewModel;
        Background = (IBrush?)Application.Current?.Resources["SurfacePanelBrush"] ?? Brushes.Transparent;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;

        // ── Editor controls ───────────────────────────────────────────
        _codeFontFamily = new TextBox
        {
            Text = viewModel.Candidate.Editor.CodeFontFamily,
            PlaceholderText = "e.g. Cascadia Code, Consolas, monospace"
        };
        _codeFontSize = new TextBox
        {
            Text = viewModel.Candidate.Editor.CodeFontSize.ToString(),
            PlaceholderText = "14"
        };
        _proseFontFamily = new TextBox
        {
            Text = viewModel.Candidate.Editor.ProseFontFamily,
            PlaceholderText = "e.g. Georgia, serif"
        };
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

        // ── Terminal controls ──────────────────────────────────────────
        _terminalFontFamily = new TextBox
        {
            Text = viewModel.Candidate.Editor.TerminalFontFamily,
            PlaceholderText = "e.g. Cascadia Code, JetBrains Mono, monospace"
        };
        _terminalFontSize = new TextBox
        {
            Text = viewModel.Candidate.Editor.TerminalFontSize.ToString(),
            PlaceholderText = "14"
        };

        // ── LLM controls ───────────────────────────────────────────────
        _model = new TextBox { Text = viewModel.Candidate.Llm.Model, PlaceholderText = "Model" };
        _baseUrl = new TextBox { Text = viewModel.Candidate.Llm.BaseUrl, PlaceholderText = "Base URL" };
        _apiKey = new TextBox { Text = viewModel.ApiKey ?? "", PasswordChar = '•', PlaceholderText = "API key" };

        // ── Status displays ────────────────────────────────────────────
        _errors = TextStyles.Caption("");
        _errors.Foreground = Brushes.OrangeRed;
        _conflict = TextStyles.Caption("");
        _conflict.Foreground = Brushes.Orange;

        // ── Button bar ─────────────────────────────────────────────────
        var apply = new Button { Content = "Apply", Command = viewModel.ApplyCommand };
        var rebase = new Button { Content = "Rebase / Refresh", Command = viewModel.RebaseCommand };
        var discard = new Button { Content = "Discard", Command = viewModel.DiscardCommand };
        var close = new Button { Content = "Close", Command = viewModel.CloseCommand };

        // ── Wire events ─────────────────────────────────────────────────
        // Editor text fields
        _codeFontFamily.TextChanged += (_, _) => { if (!_syncing) viewModel.SetCodeFontFamily(_codeFontFamily.Text ?? ""); };
        _codeFontSize.TextChanged += (_, _) => { if (!_syncing && int.TryParse(_codeFontSize.Text, out var s)) viewModel.SetCodeFontSize(s); };
        _proseFontFamily.TextChanged += (_, _) => { if (!_syncing) viewModel.SetProseFontFamily(_proseFontFamily.Text ?? ""); };
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
        // Terminal text fields
        _terminalFontFamily.TextChanged += (_, _) => { if (!_syncing) viewModel.SetTerminalFontFamily(_terminalFontFamily.Text ?? ""); };
        _terminalFontSize.TextChanged += (_, _) => { if (!_syncing && int.TryParse(_terminalFontSize.Text, out var s)) viewModel.SetTerminalFontSize(s); };
        // LLM text fields
        _model.TextChanged += (_, _) => { if (!_syncing) viewModel.SetModel(_model.Text ?? ""); };
        _baseUrl.TextChanged += (_, _) => { if (!_syncing) viewModel.SetBaseUrl(_baseUrl.Text ?? ""); };
        _apiKey.TextChanged += (_, _) => { if (!_syncing) viewModel.ApiKey = _apiKey.Text; };

        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // ── Build layout ────────────────────────────────────────────────
        var editorSection = BuildSection("Editor",
            LabelledField("Code Font Family", _codeFontFamily),
            LabelledField("Code Font Size", _codeFontSize),
            LabelledField("Prose Font Family", _proseFontFamily),
            LabelledField("Tab Size", _tabSize),
            _insertSpaces, _showWhitespace, _showTabs, _showSpaces);

        var terminalSection = BuildSection("Terminal",
            LabelledField("Terminal Font Family", _terminalFontFamily),
            LabelledField("Terminal Font Size", _terminalFontSize));

        var llmSection = BuildSection("LLM",
            LabelledField("Model", _model),
            LabelledField("Base URL", _baseUrl),
            LabelledField("API Key", _apiKey));

        Content = new Border
        {
            Padding = LayoutTokens.Inset(LayoutTokens.SpacingXl, LayoutTokens.SpacingXl, LayoutTokens.SpacingXl, LayoutTokens.SpacingXl),
            Child = new StackPanel
            {
                Width = 520,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = LayoutTokens.SpacingMd,
                Children =
                {
                    TextStyles.Header("Settings"),
                    TextStyles.Caption("Saved settings. Environment variables may override runtime values."),
                    editorSection,
                    terminalSection,
                    llmSection,
                    _conflict, _errors,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = LayoutTokens.SpacingSm,
                        Children = { apply, rebase, discard, close }
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
        _codeFontFamily.Text = ViewModel.Candidate.Editor.CodeFontFamily;
        _codeFontSize.Text = ViewModel.Candidate.Editor.CodeFontSize.ToString();
        _proseFontFamily.Text = ViewModel.Candidate.Editor.ProseFontFamily;
        _tabSize.Text = ViewModel.Candidate.Editor.TabSize.ToString();
        _insertSpaces.IsChecked = ViewModel.Candidate.Editor.InsertSpaces;
        _showWhitespace.IsChecked = ViewModel.Candidate.Editor.ShowWhitespace;
        _showTabs.IsChecked = ViewModel.Candidate.Editor.ShowTabs;
        _showSpaces.IsChecked = ViewModel.Candidate.Editor.ShowSpaces;
        _terminalFontFamily.Text = ViewModel.Candidate.Editor.TerminalFontFamily;
        _terminalFontSize.Text = ViewModel.Candidate.Editor.TerminalFontSize.ToString();
        _model.Text = ViewModel.Candidate.Llm.Model;
        _baseUrl.Text = ViewModel.Candidate.Llm.BaseUrl;
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
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = (IBrush?)Application.Current?.Resources["TextPrimaryBrush"]
                        ?? new SolidColorBrush(Color.Parse("#E3E4F4")),
            Margin = new Thickness(0, LayoutTokens.SpacingSm, 0, LayoutTokens.SpacingXxs)
        });
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
