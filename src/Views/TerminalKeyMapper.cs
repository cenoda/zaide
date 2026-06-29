using System;
using Avalonia.Input;

namespace Zaide.Views;

/// <summary>
/// Pure key-mapping helper that translates Avalonia <see cref="Key"/> and
/// <see cref="KeyModifiers"/> into the byte sequences a PTY shell expects.
///
/// <para>Extracted from <see cref="TerminalPanel"/> so the mapping is testable
/// without instantiating any Avalonia controls.</para>
/// </summary>
public static class TerminalKeyMapper
{
    /// <summary>
    /// Maps a key press to a PTY byte sequence, or <c>null</c> when no mapping
    /// applies (e.g. plain letters that should be handled by
    /// <see cref="InputElement.TextInputEvent"/> instead).
    /// </summary>
    /// <param name="key">The physical or virtual key that was pressed.</param>
    /// <param name="modifiers">The modifier keys held during the press.</param>
    /// <returns>
    /// A byte array to write to the PTY, or <c>null</c> when the key should
    /// fall through to normal text input.
    /// </returns>
    public static byte[]? Map(Key key, KeyModifiers modifiers)
    {
        bool ctrl = modifiers.HasFlag(KeyModifiers.Control);
        bool alt = modifiers.HasFlag(KeyModifiers.Alt);
        bool shift = modifiers.HasFlag(KeyModifiers.Shift);

        // Ctrl+Shift+C / Ctrl+Shift+V are reserved for clipboard — handled by
        // the View layer, not forwarded to the PTY.
        if (ctrl && shift && (key == Key.C || key == Key.V))
            return null;

        // When Ctrl (without Alt/Shift) is held, map common control keys.
        if (ctrl && !alt && !shift)
        {
            return key switch
            {
                Key.C => new byte[] { 0x03 }, // Ctrl+C — SIGINT / cancel
                Key.D => new byte[] { 0x04 }, // Ctrl+D — EOF / exit
                Key.L => new byte[] { 0x0C }, // Ctrl+L — form feed / clear
                _ => null,
            };
        }

        // Non-control keys are only mapped when no modifiers are held.
        // This prevents Shift+Enter, Ctrl+Shift+Enter, Alt+arrows, etc.
        // from silently collapsing into plain terminal input, keeping
        // those combinations available for future View-level actions
        // (clipboard, pane splitting, etc.).
        if (modifiers != KeyModifiers.None)
            return null;

        return key switch
        {
            Key.Enter => new byte[] { (byte)'\r' },
            Key.Back => new byte[] { 0x7F }, // DEL character
            Key.Tab => new byte[] { 0x09 },

            // Cursor arrows
            Key.Left => "\x1B[D"u8.ToArray(),
            Key.Right => "\x1B[C"u8.ToArray(),
            Key.Up => "\x1B[A"u8.ToArray(),
            Key.Down => "\x1B[B"u8.ToArray(),

            // Navigation
            Key.Home => "\x1B[H"u8.ToArray(),
            Key.End => "\x1B[F"u8.ToArray(),
            Key.Delete => "\x1B[3~"u8.ToArray(),

            // Deferred keys — recognised but not yet mapped (return null).
            // Escape, PageUp, PageDown, and F-keys are candidates if shell
            // usage increases.
            _ => null,
        };
    }
}
