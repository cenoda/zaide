using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Zaide.UI.DesignSystem;
using Zaide.ViewModels;

namespace Zaide.Views;

/// <summary>
/// Unsaved-changes dialog. Three buttons:
/// Save → Close(true), Don't Save → Close(false), Cancel → Close(null).
/// DataContext is set to the EditorViewModel of the tab being closed.
/// </summary>
public partial class UnsavedDialog : Window
{
    public UnsavedDialog()
    {
        InitializeComponent();

        // SpacingXl is an x:Double token; Layoutable.Margin requires Thickness.
        // Binding Margin="{StaticResource SpacingXl}" throws InvalidCastException
        // in compiled Avalonia XAML (see ISSUE-007).
        RootPanel.Margin = LayoutTokens.Uniform(LayoutTokens.SpacingXl);

        // M1.5: Enable backdrop blur on the dialog window.
        // Priority order matches MainWindow: AcrylicBlur → Blur → Transparent.
        // On platforms without compositor support, falls back to a solid
        // SurfaceRaisedBrush background (no blur, still looks intentional).
        TransparencyLevelHint = new[]
        {
            WindowTransparencyLevel.AcrylicBlur,
            WindowTransparencyLevel.Blur,
            WindowTransparencyLevel.Transparent
        };

        SaveButton.Click += OnSave;
        DontSaveButton.Click += OnDontSave;
        CancelButton.Click += OnCancel;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is EditorViewModel vm)
            MessageText.Text =
                $"Do you want to save the changes you made to '{vm.FileName}'?";
    }

    private void OnSave(object? sender, RoutedEventArgs e) => Close(true);
    private void OnDontSave(object? sender, RoutedEventArgs e) => Close(false);
    private void OnCancel(object? sender, RoutedEventArgs e) => Close((bool?)null);
}
