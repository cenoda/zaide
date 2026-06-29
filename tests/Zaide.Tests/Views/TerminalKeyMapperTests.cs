using System;
using Avalonia.Input;
using Xunit;
using Zaide.Views;

namespace Zaide.Tests.Views;

/// <summary>
/// Unit tests for <see cref="TerminalKeyMapper"/>. All tests are pure — no
/// Avalonia controls are instantiated.
/// </summary>
public class TerminalKeyMapperTests
{
    // --- Enter ---

    [Fact]
    public void Maps_Enter()
    {
        byte[]? result = TerminalKeyMapper.Map(Key.Enter, KeyModifiers.None);
        Assert.Equal(new byte[] { (byte)'\r' }, result);
    }

    // --- Backspace ---

    [Fact]
    public void Maps_Backspace()
    {
        byte[]? result = TerminalKeyMapper.Map(Key.Back, KeyModifiers.None);
        Assert.Equal(new byte[] { 0x7F }, result);
    }

    // --- Tab ---

    [Fact]
    public void Maps_Tab()
    {
        byte[]? result = TerminalKeyMapper.Map(Key.Tab, KeyModifiers.None);
        Assert.Equal(new byte[] { 0x09 }, result);
    }

    // --- Cursor arrows ---

    [Theory]
    [InlineData(Key.Left, "\x1B[D")]
    [InlineData(Key.Right, "\x1B[C")]
    [InlineData(Key.Up, "\x1B[A")]
    [InlineData(Key.Down, "\x1B[B")]
    public void Maps_Arrows(Key key, string expected)
    {
        byte[]? result = TerminalKeyMapper.Map(key, KeyModifiers.None);
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes(expected), result);
    }

    // --- Navigation ---

    [Fact]
    public void Maps_Home()
    {
        byte[]? result = TerminalKeyMapper.Map(Key.Home, KeyModifiers.None);
        Assert.Equal("\x1B[H"u8.ToArray(), result);
    }

    [Fact]
    public void Maps_End()
    {
        byte[]? result = TerminalKeyMapper.Map(Key.End, KeyModifiers.None);
        Assert.Equal("\x1B[F"u8.ToArray(), result);
    }

    [Fact]
    public void Maps_Delete()
    {
        byte[]? result = TerminalKeyMapper.Map(Key.Delete, KeyModifiers.None);
        Assert.Equal("\x1B[3~"u8.ToArray(), result);
    }

    // --- Control keys ---

    [Theory]
    [InlineData(Key.C, new byte[] { 0x03 })]
    [InlineData(Key.D, new byte[] { 0x04 })]
    [InlineData(Key.L, new byte[] { 0x0C })]
    public void Maps_CtrlLetter(Key key, byte[] expected)
    {
        byte[]? result = TerminalKeyMapper.Map(key, KeyModifiers.Control);
        Assert.Equal(expected, result);
    }

    // --- Ctrl+Shift clipboard keys (reserved, return null) ---

    [Theory]
    [InlineData(Key.C)]
    [InlineData(Key.V)]
    public void ReturnsNull_ForCtrlShiftClipboard(Key key)
    {
        byte[]? result = TerminalKeyMapper.Map(key, KeyModifiers.Control | KeyModifiers.Shift);
        Assert.Null(result);
    }

    // --- Plain letters (no modifiers) fall through ---

    [Theory]
    [InlineData(Key.C)]
    [InlineData(Key.D)]
    [InlineData(Key.L)]
    [InlineData(Key.A)]
    [InlineData(Key.Z)]
    public void ReturnsNull_ForPlainLetter(Key key)
    {
        byte[]? result = TerminalKeyMapper.Map(key, KeyModifiers.None);
        Assert.Null(result);
    }

    // --- Deferred keys ---

    [Theory]
    [InlineData(Key.Escape)]
    [InlineData(Key.PageUp)]
    [InlineData(Key.PageDown)]
    [InlineData(Key.F1)]
    [InlineData(Key.F2)]
    [InlineData(Key.F3)]
    [InlineData(Key.F4)]
    [InlineData(Key.F5)]
    [InlineData(Key.F6)]
    [InlineData(Key.F7)]
    [InlineData(Key.F8)]
    [InlineData(Key.F9)]
    [InlineData(Key.F10)]
    [InlineData(Key.F11)]
    [InlineData(Key.F12)]
    public void ReturnsNull_ForDeferredKeys(Key key)
    {
        byte[]? result = TerminalKeyMapper.Map(key, KeyModifiers.None);
        Assert.Null(result);
    }

    // --- Modifier guard: base keys return null when modifiers are held ---
    // These combinations must not silently collapse into terminal input
    // so they remain available for future View-level actions (clipboard,
    // pane splitting, etc.).

    [Theory]
    [InlineData(Key.Enter, KeyModifiers.Shift)]
    [InlineData(Key.Enter, KeyModifiers.Control | KeyModifiers.Shift)]
    [InlineData(Key.Enter, KeyModifiers.Alt)]
    [InlineData(Key.Back, KeyModifiers.Shift)]
    [InlineData(Key.Tab, KeyModifiers.Shift)]
    [InlineData(Key.Left, KeyModifiers.Alt)]
    [InlineData(Key.Home, KeyModifiers.Shift)]
    [InlineData(Key.Delete, KeyModifiers.Shift)]
    public void ReturnsNull_WhenModifierHeld(Key key, KeyModifiers modifiers)
    {
        byte[]? result = TerminalKeyMapper.Map(key, modifiers);
        Assert.Null(result);
    }
}
