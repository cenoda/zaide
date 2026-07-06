using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xunit;
using Zaide.Views;

namespace Zaide.Tests.Views;

public class TownhallInputAreaTests
{
    static TownhallInputAreaTests()
    {
        EnsureApplication();
    }

    [Fact]
    public void InputField_AcceptsReturn_IsTrue()
    {
        var inputArea = new TownhallInputArea();

        Assert.True(GetInputField(inputArea).AcceptsReturn);
    }

    [Fact]
    public void InputField_TextWrapping_IsWrap()
    {
        var inputArea = new TownhallInputArea();

        Assert.Equal(TextWrapping.Wrap, GetInputField(inputArea).TextWrapping);
    }

    [Fact]
    public void InputField_MaxLines_IsFive()
    {
        var inputArea = new TownhallInputArea();

        Assert.Equal(5, GetInputField(inputArea).MaxLines);
    }

    [Fact]
    public void EnterKey_TriggersSend()
    {
        var inputArea = new TownhallInputArea();
        var inputField = GetInputField(inputArea);
        inputArea.InputText = "hello";
        var sendCount = 0;
        inputArea.SendRequested += () => sendCount++;

        inputField.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Source = inputField,
            Key = Key.Enter,
            KeyModifiers = KeyModifiers.None
        });

        Assert.Equal(1, sendCount);
    }

    [Fact]
    public void ShiftEnterKey_DoesNotTriggerSend()
    {
        var inputArea = new TownhallInputArea();
        var inputField = GetInputField(inputArea);
        inputArea.InputText = "hello";
        var sendCount = 0;
        inputArea.SendRequested += () => sendCount++;

        inputField.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Source = inputField,
            Key = Key.Enter,
            KeyModifiers = KeyModifiers.Shift
        });

        Assert.Equal(0, sendCount);
    }

    private static TextBox GetInputField(TownhallInputArea inputArea)
    {
        return inputArea.GetVisualDescendants().OfType<TextBox>().Single();
    }

    private static void EnsureApplication()
    {
        if (Application.Current is App app)
        {
            if (!app.Resources.ContainsKey("PrimaryAccentBrush"))
            {
                app.Initialize();
            }

            return;
        }

        var createdApp = new App();
        createdApp.Initialize();
    }
}
