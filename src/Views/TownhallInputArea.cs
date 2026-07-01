using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Zaide.Views;

public class TownhallInputArea : UserControl
{
    private readonly TextBox _inputBox;

    public static readonly StyledProperty<string> DraftTextProperty =
        AvaloniaProperty.Register<TownhallInputArea, string>(nameof(DraftText), string.Empty);

    public string DraftText
    {
        get => GetValue(DraftTextProperty);
        set => SetValue(DraftTextProperty, value);
    }

    public event Action<string>? DraftChanged;
    public event Action? SendClicked;

    public TownhallInputArea()
    {
        _inputBox = new TextBox
        {
            PlaceholderText = "Message #townhall-main",
            FontSize = 13,
            Background = (IBrush?)Application.Current!.Resources["SurfacePanel"],
            Foreground = (IBrush?)Application.Current!.Resources["TextActive"],
            BorderBrush = (IBrush?)Application.Current!.Resources["SurfaceBorder"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            MinHeight = 34
        };

        // Send button — icon-like triangle
        var sendIcon = new TextBlock
        {
            Text = "▶",
            FontSize = 11,
            Foreground = (IBrush?)Application.Current!.Resources["TextActive"],
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var sendButton = new Border
        {
            Width = 34,
            Height = 34,
            Background = (IBrush?)Application.Current!.Resources["PrimaryAccent"],
            CornerRadius = new CornerRadius(6),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = sendIcon,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        sendButton.PointerPressed += (_, e) =>
        {
            if (sendButton.IsVisible)
                SendClicked?.Invoke();
            e.Handled = true;
        };

        _inputBox.GetObservable(TextBox.TextProperty).Subscribe(text =>
        {
            var value = text ?? string.Empty;
            if (DraftText != value)
            {
                DraftText = value;
                DraftChanged?.Invoke(value);
            }
        });

        var root = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Background = (IBrush?)Application.Current!.Resources["SurfaceBase"],
            Margin = new Thickness(0),
            ColumnSpacing = 8
        };

        Grid.SetColumn(_inputBox, 0);
        Grid.SetColumn(sendButton, 1);
        root.Children.Add(_inputBox);
        root.Children.Add(sendButton);

        var host = new Border
        {
            BorderBrush = (IBrush?)Application.Current!.Resources["SurfaceBorder"],
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(12, 10, 12, 10),
            Background = (IBrush?)Application.Current!.Resources["SurfaceBase"],
            Child = root
        };

        Content = host;

        this.GetObservable(DraftTextProperty).Subscribe(value =>
        {
            if (_inputBox.Text != value)
                _inputBox.Text = value;
        });
    }
}