using System;
using Avalonia.Input;
using Zaide.Services;

namespace Zaide.Views;

/// <summary>
/// UI-layer helper that converts neutral <see cref="ResolvedKeyBinding"/> records
/// into Avalonia <see cref="KeyBinding"/> instances. Keeps the service/registry
/// layer framework-agnostic — the registry only produces <see cref="ResolvedKeyBinding"/>.
/// </summary>
internal static class KeyBindingConverter
{
    /// <summary>
    /// Parse a normalized gesture string (e.g. "Ctrl+S") into an Avalonia <see cref="KeyGesture"/>.
    /// The input must be a valid normalized gesture produced by <see cref="CommandRegistry"/> resolution.
    /// </summary>
    internal static KeyGesture ParseToKeyGesture(string normalizedGesture)
    {
        var parts = normalizedGesture.Split('+');
        var modifiers = KeyModifiers.None;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            modifiers |= parts[i] switch
            {
                "Ctrl" => KeyModifiers.Control,
                "Alt" => KeyModifiers.Alt,
                "Shift" => KeyModifiers.Shift,
                "Meta" => KeyModifiers.Meta,
                _ => KeyModifiers.None,
            };
        }

        var key = Enum.Parse<Key>(parts[^1]);
        return new KeyGesture(key, modifiers);
    }

    /// <summary>
    /// Create an Avalonia <see cref="KeyBinding"/> from a neutral <see cref="ResolvedKeyBinding"/>
    /// and its <see cref="CommandDescriptor"/>. Returns null when descriptor is null.
    /// </summary>
    internal static KeyBinding? TryCreateKeyBinding(ResolvedKeyBinding resolved, CommandDescriptor? descriptor)
    {
        if (descriptor is null)
            return null;

        return new KeyBinding
        {
            Gesture = ParseToKeyGesture(resolved.Gesture),
            Command = descriptor.Command,
        };
    }
}
