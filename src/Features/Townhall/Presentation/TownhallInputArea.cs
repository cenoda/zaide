using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using System;
using System.Threading.Tasks;
using Zaide.UI.DesignSystem;
using Zaide.App.Shell;

namespace Zaide.Features.Townhall.Presentation;

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

    /// <summary>
    /// Enables or disables user input and send affordances.
    /// </summary>
    public bool IsInputEnabled
    {
        get => _inputField.IsEnabled;
        set
        {
            _inputField.IsEnabled = value;
            _sendButton.IsHitTestVisible = value;
            _sendButton.Opacity = value ? 1.0 : 0.5;
        }
    }

    public TownhallInputArea()
    {
        // Input text box
        _inputField = new TextBox
        {
            PlaceholderText = "Message...",
            Background = new SolidColorBrush(Color.FromArgb(0x0D, 0xFF, 0xFF, 0xFF)),
            Foreground = PaletteTokens.TextPrimaryBrush,
            BorderThickness = new Thickness(0),
            MinHeight = 32,
            MaxLines = 5,
            // AcceptsReturn is intentionally false: when true, the TextBox class
            // handler consumes Enter (inserts a newline and marks the KeyDown
            // event handled) before our instance handler runs, so Enter would
            // never trigger a send. We handle Enter/Shift+Enter ourselves below.
            AcceptsReturn = false,
            TextWrapping = TextWrapping.Wrap,
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingMd, LayoutTokens.SpacingSm)
        };
        AutomationProperties.SetName(_inputField, "Townhall message input");
        AutomationProperties.SetHelpText(
            _inputField,
            "Type a message. Press Enter to send. Press Shift+Enter for a new line.");

        // Send button (arrow icon)
        var sendIcon = CreateIconOrFallback(
            "Icon.ArrowUp",
            "↑",
            PaletteTokens.TextPrimaryBrush,
            14);

        _sendButton = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = LayoutTokens.RadiusFull,
            Background = PaletteTokens.PrimaryAccentBrush,
            Child = sendIcon,
            Cursor = CreateHandCursorOrNull(),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingXs, 0, 0, 0),
            RenderTransformOrigin = RelativePoint.Center,
            RenderTransform = new ScaleTransform(1, 1)
        };
        AutomationProperties.SetName(_sendButton, "Send message");
        AutomationProperties.SetHelpText(
            _sendButton,
            "Send the current Townhall message. Enter in the input field also sends.");

        // "+" attachment button (visual affordance only in Phase 14)
        var attachButton = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = LayoutTokens.RadiusFull,
            Child = CreateIconOrFallback(
                "Icon.Plus",
                "+",
                PaletteTokens.TextSecondaryBrush,
                14),
            Cursor = CreateHandCursorOrNull(),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = LayoutTokens.Inset(0, 0, LayoutTokens.SpacingXs, 0)
        };
        AutomationProperties.SetName(attachButton, "Attachment (unavailable)");
        AutomationProperties.SetHelpText(
            attachButton,
            "Attachment actions are not available in Phase 14.");

        // Layout: [+ button] [send button] [input field (fills)]
        // DockPanel: the LAST child always fills remaining space.
        // Input field must be last so it stretches to fill; send button
        // gets Dock.Right to stay at a fixed button width.
        var inputRow = new DockPanel
        {
            VerticalAlignment = VerticalAlignment.Center
        };

        DockPanel.SetDock(attachButton, Dock.Left);
        DockPanel.SetDock(_sendButton, Dock.Right);

        inputRow.Children.Add(attachButton);
        inputRow.Children.Add(_sendButton);
        inputRow.Children.Add(_inputField);

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

        // Enter sends; Shift+Enter inserts a newline at the caret.
        // AcceptsReturn is false (see above), so the TextBox never consumes
        // Enter and this handler always runs first.
        _inputField.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;

            e.Handled = true;

            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                InsertNewlineAtCaret();
            }
            else
            {
                TriggerSend();
            }
        };

        // Propagate text changes for bidirectional draft sync
        _inputField.TextChanged += (_, args) => TextChanged?.Invoke(this, args);
    }

    private void TriggerSend()
    {
        if (!IsInputEnabled)
        {
            return;
        }

        var text = InputText.Trim();
        if (string.IsNullOrEmpty(text))
            return;

        SendRequested?.Invoke();
    }

    /// <summary>
    /// Inserts a newline at the current caret position. Used for Shift+Enter,
    /// since AcceptsReturn is disabled (Enter is reserved for sending).
    /// </summary>
    private void InsertNewlineAtCaret()
    {
        var text = _inputField.Text ?? string.Empty;
        var caret = _inputField.CaretIndex;
        if (caret < 0 || caret > text.Length)
            caret = text.Length;

        _inputField.Text = text.Insert(caret, Environment.NewLine);
        _inputField.CaretIndex = caret + Environment.NewLine.Length;
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

        await Animations.RunAsync(transform, Animations.CreateScaleBounce(0.95d, 1d));
    }

    private static Control CreateIconOrFallback(string resourceKey, string fallbackText, IBrush foreground, double size)
    {
        if (Application.Current is { } app &&
            app.TryFindResource(resourceKey, ThemeVariant.Default, out _))
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
