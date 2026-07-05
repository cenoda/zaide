using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Reactive;
using ReactiveUI;

namespace Zaide.Views;

/// <summary>
/// Input area for the Townhall chat panel.
/// Contains a text input with placeholder, send button, and optional action icons.
/// Enter sends the message; Shift+Enter inserts newline.
/// Matches M0.5 palette and M3 spec.
/// </summary>
public class TownhallInputArea : Panel
{
    private readonly TextBox _inputField;
    private readonly Border _sendButton;

    /// <summary>
    /// Fired when the send action is triggered (Enter key or send button click).
    /// </summary>
    public event Action? SendRequested;

    /// <summary>
    /// Fired when the input text changes (for bidirectional draft sync).
    /// </summary>
    public event EventHandler? TextChanged;

    /// <summary>
    /// Gets or sets the text in the input field.
    /// </summary>
    public string InputText
    {
        get => _inputField.Text ?? string.Empty;
        set => _inputField.Text = value;
    }

    /// <summary>
    /// Gets or sets the placeholder text.
    /// </summary>
    public string PlaceholderText
    {
        get => _inputField.PlaceholderText ?? string.Empty;
        set => _inputField.PlaceholderText = value;
    }

    public TownhallInputArea()
    {
        // Input text box
        _inputField = new TextBox
        {
            PlaceholderText = "Message...",
            Background = new SolidColorBrush(Color.FromArgb(0x0D, 0xFF, 0xFF, 0xFF)),
            Foreground = (IBrush?)Application.Current!.Resources["TextPrimaryBrush"],
            BorderThickness = new Thickness(0),
            MinHeight = 32,
            MaxHeight = 96,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Padding = new Thickness(12, 8, 12, 8)
        };

        // Send button (arrow icon)
        var sendIcon = IconFactory.Create(
            "Icon.ArrowUp",
            (IBrush?)Application.Current!.Resources["TextPrimaryBrush"],
            14);

        _sendButton = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(9999),
            Background = (IBrush?)Application.Current!.Resources["PrimaryAccentBrush"],
            Child = sendIcon,
            Cursor = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0)
        };

        // "+" attachment button
        var attachButton = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(9999),
            Child = IconFactory.Create(
                "Icon.Plus",
                (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
                14),
            Cursor = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };

        // Layout: [+ button] [input field (fills)] [send button]
        var inputRow = new DockPanel
        {
            VerticalAlignment = VerticalAlignment.Center
        };

        DockPanel.SetDock(attachButton, Dock.Left);
        DockPanel.SetDock(_sendButton, Dock.Right);

        inputRow.Children.Add(attachButton);
        inputRow.Children.Add(_inputField);
        inputRow.Children.Add(_sendButton);

        var container = new Border
        {
            Padding = new Thickness(8),
            Child = inputRow
        };

        Children.Add(container);

        // Send button click
        _sendButton.PointerPressed += (_, _) => TriggerSend();

        // Enter to send, Shift+Enter for newline
        _inputField.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                e.Handled = true;
                TriggerSend();
            }
        };

        // Propagate text changes for bidirectional draft sync
        _inputField.TextChanged += (_, args) => TextChanged?.Invoke(this, args);
    }

    private void TriggerSend()
    {
        var text = InputText.Trim();
        if (string.IsNullOrEmpty(text))
            return;

        SendRequested?.Invoke();
    }
}
