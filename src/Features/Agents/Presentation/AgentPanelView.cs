using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Zaide.Features.Agents.Domain;
using Zaide.UI.DesignSystem;

namespace Zaide.Features.Agents.Presentation;

/// <summary>
/// Converts <see cref="AgentPanelState.IsBusy"/> (true = busy) to
/// <see cref="Control.IsEnabled"/> (false = disabled).
/// M3: Added for input-surface disable during in-flight requests.
/// </summary>
internal sealed class BusyToEnabledConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool busy)
            return !busy;
        return true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// View for a single agent panel. Displays agent name/status, output history,
/// and a draft input area.
///
/// View-layer only — no ViewModel. Binds directly to <see cref="AgentPanelState"/>
/// via its observable properties.
///
/// M2: Exposes <see cref="SendRequested"/> event for Enter-to-send.
/// M3: Input box is disabled while <see cref="AgentPanelState.IsBusy"/> is true.
/// </summary>
public sealed class AgentPanelView : ReactiveUserControl<AgentPanelState>
{
    private readonly TextBlock _headerText;
    private readonly TextBlock _statusText;
    private readonly ListBox _outputList;
    private readonly TextBox _inputBox;

    /// <summary>
    /// Raised when the user presses Enter in the input box with non-empty text.
    /// Payload is (panelId, messageText).
    /// </summary>
    public event Action<string, string>? SendRequested;

    public AgentPanelView()
    {
        // --- Header: agent name + status ---
        _headerText = TextStyles.Header("");
        _headerText.VerticalAlignment = VerticalAlignment.Center;

        _statusText = TextStyles.Caption("");
        _statusText.VerticalAlignment = VerticalAlignment.Center;
        _statusText.Margin = LayoutTokens.Inset(LayoutTokens.SpacingSm, 0, 0, 0);

        var headerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _headerText, _statusText }
        };

        var headerBorder = new Border
        {
            Background = ResolveBrush("SurfaceBaseBrush", "#121722"),
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingMd, LayoutTokens.SpacingSm),
            Child = headerPanel
        };

        // --- Output history ---
        _outputList = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingMd, LayoutTokens.SpacingSm)
        };

        // Item template for output entries
        _outputList.ItemTemplate = new FuncDataTemplate<string>((entry, _) =>
        {
            if (entry is null) return null;
            return TextStyles.Body(entry);
        });

        // --- Input area ---
        _inputBox = new TextBox
        {
            FontSize = 13,
            PlaceholderText = "Type a message...",
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingSm, LayoutTokens.SpacingXs),
            BorderThickness = new Thickness(1),
            CornerRadius = LayoutTokens.RadiusSm,
            AcceptsReturn = false,
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingMd, 0, LayoutTokens.SpacingMd, LayoutTokens.SpacingMd)
        };

        // M2: Enter-to-send trigger
        _inputBox.KeyDown += OnInputBoxKeyDown;

        // M3: Disable input while busy (IsBusy=true → IsEnabled=false)
        _inputBox.Bind(
            TextBox.IsEnabledProperty,
            new Binding(nameof(AgentPanelState.IsBusy))
            {
                Converter = new BusyToEnabledConverter()
            });

        var inputBorder = new Border
        {
            Background = ResolveBrush("SurfaceBaseBrush", "#121722"),
            BorderThickness = new Thickness(0),
            Child = _inputBox
        };

        // --- Layout ---
        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto }
            },
            Background = ResolveBrush("SurfacePanelBrush", "#0B0F17"),
            Children = { headerBorder, _outputList, inputBorder }
        };
        Grid.SetRow(headerBorder, 0);
        Grid.SetRow(_outputList, 1);
        Grid.SetRow(inputBorder, 2);

        Content = root;

        // --- Bindings ---
        this.WhenActivated(d =>
        {
            d.Add(this.OneWayBind(ViewModel, vm => vm.AgentName, v => v._headerText.Text));
            d.Add(this.OneWayBind(ViewModel, vm => vm.Status, v => v._statusText.Text));
            d.Add(this.OneWayBind(ViewModel, vm => vm.OutputHistory, v => v._outputList.ItemsSource));
            d.Add(this.Bind(ViewModel, vm => vm.DraftInput, v => v._inputBox.Text));
        });
    }

    private void OnInputBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        e.Handled = true;

        var panelId = ViewModel?.PanelId;
        var message = ViewModel?.DraftInput;

        if (string.IsNullOrWhiteSpace(panelId))
            return;

        if (string.IsNullOrWhiteSpace(message))
            return;

        SendRequested?.Invoke(panelId, message);
    }

    private static IBrush ResolveBrush(string resourceKey, string fallbackColor)
    {
        try
        {
            if (Avalonia.Application.Current?.Resources[resourceKey] is IBrush brush)
                return brush;
        }
        catch (InvalidOperationException)
        {
            // Test hosts may construct views without a UI-thread-owned
            // Application. Use the palette fallback in that case.
        }

        return new SolidColorBrush(Color.Parse(fallbackColor));
    }
}
