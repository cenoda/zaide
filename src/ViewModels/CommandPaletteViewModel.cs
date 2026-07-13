using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using ReactiveUI;
using Zaide.Services;

namespace Zaide.ViewModels;

/// <summary>
/// UI-independent palette query/presentation seam.
/// Enumerates registered commands from <see cref="ICommandRegistry"/> with
/// deterministic ordering and case-insensitive literal substring filtering.
/// <para/>
/// Does not depend on Avalonia controls, EditorView, MainWindow, or focus APIs.
/// Query/filter/presentation only — the palette overlay, input routing, keyboard
/// navigation, focus restoration, and execution UI belong in M2.
/// </summary>
public sealed class CommandPaletteViewModel
{
    private readonly ICommandRegistry _registry;

    /// <summary>
    /// Command that opens the command palette. Always available.
    /// In M1 this is a no-op placeholder; M2 will wire it to the palette overlay.
    /// </summary>
    public ICommand OpenPaletteCommand { get; }

    public CommandPaletteViewModel(ICommandRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));

        // Register palette.open with M0-locked metadata and default gesture.
        // Owned by this ViewModel; the command body is a no-op in M1.
        OpenPaletteCommand = ReactiveCommand.Create(() => { });
        _registry.Register(new CommandDescriptor(
            "palette.open",
            "Open Command Palette",
            "Palette",
            new[] { "Ctrl+Shift+P" },
            OpenPaletteCommand));
    }

    /// <summary>
    /// Returns all registered descriptors as palette entries, ordered by
    /// category (OrdinalIgnoreCase), then display name (OrdinalIgnoreCase),
    /// then ID (OrdinalIgnoreCase).
    /// </summary>
    public IReadOnlyList<PaletteEntry> GetAllEntries()
    {
        return _registry.GetAll()
            .Select(d => new PaletteEntry(d.Id, d.DisplayName, d.Category, d.Command.CanExecute(null)))
            .OrderBy(e => e.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Filters palette entries by case-insensitive literal substring match against
    /// <see cref="PaletteEntry.DisplayName"/>. Returns all entries ordered when
    /// <paramref name="query"/> is null or empty.
    /// </summary>
    public IReadOnlyList<PaletteEntry> Filter(string? query)
    {
        if (string.IsNullOrEmpty(query))
            return GetAllEntries();

        return GetAllEntries()
            .Where(e => e.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
