using System;

namespace Zaide.App.Shell;
/// <summary>
/// Immutable projection of a command descriptor for presentation in a command palette.
/// UI-framework neutral — usable without Avalonia controls.
/// </summary>
public sealed class PaletteEntry
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Category { get; }
    public bool IsAvailable { get; }

    public PaletteEntry(string id, string displayName, string category, bool isAvailable)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Category = category ?? throw new ArgumentNullException(nameof(category));
        IsAvailable = isAvailable;
    }
}
