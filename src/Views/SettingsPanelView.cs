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

/// <summary>Full-content slide-over surface for transient settings editing.</summary>
public sealed class SettingsPanelView : ReactiveUserControl<SettingsViewModel>, IDisposable
{
    private readonly SettingsViewModel _settingsViewModel;
    private readonly TextBlock _errors;
    private readonly TextBox _model;
    private readonly TextBox _baseUrl;
    private readonly TextBox _apiKey;
    private readonly TextBlock _conflict;
    private bool _syncing;
    private bool _disposed;

    public SettingsPanelView(SettingsViewModel viewModel)
    {
        _settingsViewModel = viewModel;
        ViewModel = viewModel;
        Background = (IBrush?)Application.Current?.Resources["SurfacePanelBrush"] ?? Brushes.Transparent;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        _model = new TextBox { Text = viewModel.Candidate.Llm.Model, PlaceholderText = "Model" };
        _baseUrl = new TextBox { Text = viewModel.Candidate.Llm.BaseUrl, PlaceholderText = "Base URL" };
        _apiKey = new TextBox { Text = viewModel.ApiKey ?? "", PasswordChar = '•', PlaceholderText = "API key" };
        _errors = TextStyles.Caption("");
        _errors.Foreground = Brushes.OrangeRed;
        _conflict = TextStyles.Caption("");
        _conflict.Foreground = Brushes.Orange;

        var apply = new Button { Content = "Apply", Command = viewModel.ApplyCommand };
        var discard = new Button { Content = "Discard", Command = viewModel.DiscardCommand };
        var rebase = new Button { Content = "Rebase / Refresh", Command = viewModel.RebaseCommand };
        var close = new Button { Content = "Close", Command = viewModel.CloseCommand };
        _model.TextChanged += (_, _) => { if (!_syncing) viewModel.SetModel(_model.Text ?? ""); };
        _baseUrl.TextChanged += (_, _) => { if (!_syncing) viewModel.SetBaseUrl(_baseUrl.Text ?? ""); };
        _apiKey.TextChanged += (_, _) => { if (!_syncing) viewModel.ApiKey = _apiKey.Text; };
        viewModel.PropertyChanged += OnViewModelPropertyChanged;

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
                    _model, _baseUrl, _apiKey, _conflict, _errors,
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
}
