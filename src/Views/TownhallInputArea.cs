using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using System;
using System.Threading.Tasks;
using Zaide.Styles;

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
            Foreground = (IBrush?)Application.Current?.Resources["TextPrimaryBrush"] ?? new SolidColorBrush(Color.Parse("#E3E4F4")),
            BorderThickness = new Thickness(0),
            MinHeight = 32,
            MaxLines = 5,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingMd, LayoutTokens.SpacingSm)
        };

        // Send button (arrow icon)
        var sendIcon = CreateIconOrFallback(
            "Icon.ArrowUp",
            "↑",
            (IBrush?)Application.Current?.Resources["TextPrimaryBrush"] ?? new SolidColorBrush(Color.Parse("#E3E4F4")),
            14);

        _sendButton = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = LayoutTokens.RadiusFull,
            Background = (IBrush?)Application.Current?.Resources["PrimaryAccentBrush"] ?? new SolidColorBrush(Color.Parse("#066ADB")),
            Child = sendIcon,
            Cursor = CreateHandCursorOrNull(),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingXs, 0, 0, 0),
            RenderTransformOrigin = RelativePoint.Center,
            RenderTransform = new ScaleTransform(1, 1)
        };

        // "+" attachment button
        var attachButton = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = LayoutTokens.RadiusFull,
            Child = CreateIconOrFallback(
                "Icon.Plus",
                "+",
                (IBrush?)Application.Current?.Resources["TextSecondaryBrush"] ?? new SolidColorBrush(Color.Parse("#8B95A5")),
                14),
            Cursor = CreateHandCursorOrNull(),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = LayoutTokens.Inset(0, 0, LayoutTokens.SpacingXs, 0)
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

        var hintLabel = TextStyles.Caption("⏎ to send · ⇧⏎ for newline");
        hintLabel.Margin = LayoutTokens.Inset(
            LayoutTokens.SpacingXxl + LayoutTokens.SpacingSm,
            LayoutTokens.SpacingXs,
            0,
            0);

        var contentStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Children =
            {
                inputRow,
                hintLabel
            }
        };

        var container = new Border
        {
            Padding = LayoutTokens.Uniform(LayoutTokens.SpacingSm),
            Child = contentStack
        };

        Children.Add(container);

        // Send button click
        _sendButton.PointerPressed += (_, _) =>
        {
            _ = AnimateSendButtonPressAsync();
            TriggerSend();
        };

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

    private async Task AnimateSendButtonPressAsync()
    {
        if (_sendButton.RenderTransform is not ScaleTransform transform)
        {
            transform = new ScaleTransform(1, 1);
            _sendButton.RenderTransform = transform;
        }

        transform.ScaleX = 0.95;
        transform.ScaleY = 0.95;

        await Animations.CreateScaleBounce(0.95d, 1d).RunAsync(transform);
    }

    private static Control CreateIconOrFallback(string resourceKey, string fallbackText, IBrush foreground, double size)
    {
        if (Application.Current is { } app &&
            app.TryFindResource(resourceKey, app.ActualThemeVariant, out _))
        {
            return IconFactory.Create(resourceKey, foreground, size);
        }

        var fallback = TextStyles.Caption(fallbackText);
        fallback.FontSize = size;
        fallback.Foreground = foreground;
        fallback.HorizontalAlignment = HorizontalAlignment.Center;
        fallback.VerticalAlignment = VerticalAlignment.Center;
        return fallback;
    }

    private static Cursor? CreateHandCursorOrNull()
    {
        try
        {
            return new Cursor(StandardCursorType.Hand);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
