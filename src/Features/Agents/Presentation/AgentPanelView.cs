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
    private static readonly IBrush SurfaceBaseBackgroundBrush =
        PaletteTokens.GetBrush("SurfaceBaseBrush", new SolidColorBrush(Color.Parse("#121722")));

    private static readonly IBrush SurfacePanelBackgroundBrush =
        PaletteTokens.GetBrush("SurfacePanelBrush", new SolidColorBrush(Color.Parse("#0B0F17")));

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
        _headerText = TextStyles.Header("");
        _headerText.VerticalAlignment = VerticalAlignment.Center;

        _statusText = TextStyles.Caption("");
        _statusText.VerticalAlignment = VerticalAlignment.Center;
        _statusText.Margin = LayoutTokens.Inset(LayoutTokens.SpacingSm, 0, 0, 0);

        _outputList = BuildOutputList();
        _inputBox = BuildInputBox();

        Content = BuildRootLayout(BuildHeaderBorder(), BuildInputBorder());

        // --- Bindings ---
        this.WhenActivated(d =>
        {
            d.Add(this.OneWayBind(ViewModel, vm => vm.AgentName, v => v._headerText.Text));
            d.Add(this.OneWayBind(ViewModel, vm => vm.Status, v => v._statusText.Text));
            d.Add(this.OneWayBind(ViewModel, vm => vm.OutputHistory, v => v._outputList.ItemsSource));
            d.Add(this.Bind(ViewModel, vm => vm.DraftInput, v => v._inputBox.Text));
        });
    }

    /// <summary>
    /// Builds the header border: agent name and status on a raised surface.
    /// </summary>
    private Border BuildHeaderBorder()
    {
        var headerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _headerText, _statusText }
        };

        return new Border
        {
            Background = SurfaceBaseBackgroundBrush,
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingMd, LayoutTokens.SpacingSm),
            Child = headerPanel
        };
    }

    /// <summary>
    /// Builds the scrollable output history list.
    /// </summary>
    private ListBox BuildOutputList()
    {
        var outputList = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingMd, LayoutTokens.SpacingSm)
        };

        outputList.ItemTemplate = new FuncDataTemplate<string>((entry, _) =>
        {
            if (entry is null) return null;
            return TextStyles.Body(entry);
        });

        return outputList;
    }

    /// <summary>
    /// Builds the draft input text box with Enter-to-send and busy-disable binding.
    /// </summary>
    private TextBox BuildInputBox()
    {
        var inputBox = new TextBox
        {
            FontSize = 13,
            PlaceholderText = "Type a message...",
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingSm, LayoutTokens.SpacingXs),
            BorderThickness = new Thickness(1),
            CornerRadius = LayoutTokens.RadiusSm,
            AcceptsReturn = false,
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingMd, 0, LayoutTokens.SpacingMd, LayoutTokens.SpacingMd)
        };

        inputBox.KeyDown += OnInputBoxKeyDown;

        inputBox.Bind(
            TextBox.IsEnabledProperty,
            new Binding(nameof(AgentPanelState.IsBusy))
            {
                Converter = new BusyToEnabledConverter()
            });

        return inputBox;
    }

    /// <summary>
    /// Builds the input area border wrapping the draft text box.
    /// </summary>
    private Border BuildInputBorder()
    {
        return new Border
        {
            Background = SurfaceBaseBackgroundBrush,
            BorderThickness = new Thickness(0),
            Child = _inputBox
        };
    }

    /// <summary>
    /// Builds the root layout grid: header | output | input.
    /// </summary>
    private Grid BuildRootLayout(Border headerBorder, Border inputBorder)
    {
        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto }
            },
            Background = SurfacePanelBackgroundBrush,
            Children = { headerBorder, _outputList, inputBorder }
        };
        Grid.SetRow(headerBorder, 0);
        Grid.SetRow(_outputList, 1);
        Grid.SetRow(inputBorder, 2);

        return root;
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
}
