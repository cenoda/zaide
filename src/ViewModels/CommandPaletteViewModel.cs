using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using ReactiveUI;
using Zaide.Services;
using Zaide.Features.Editor.Presentation;

namespace Zaide.ViewModels;

/// <summary>
/// UI-independent palette query/presentation seam with M2 selection and lifecycle state.
/// Enumerates registered commands from <see cref="ICommandRegistry"/> with
/// deterministic ordering and case-insensitive literal substring filtering.
/// <para/>
/// Does not depend on Avalonia controls, EditorView, MainWindow, or focus APIs.
/// Query/filter/presentation/selection only — the palette overlay, input routing,
/// keyboard navigation, focus restoration belong in the View layer (M2).
/// </summary>
public sealed class CommandPaletteViewModel
{
    private readonly ICommandRegistry _registry;
    private string _query = string.Empty;
    private IReadOnlyList<PaletteEntry> _filteredEntries = Array.Empty<PaletteEntry>();
    private int _selectedIndex = -1;
    private bool _isOpen;

    /// <summary>
    /// Command that opens the command palette. Always available.
    /// Raises <see cref="OpenRequested"/> when executed.
    /// </summary>
    public ICommand OpenPaletteCommand { get; }

    /// <summary>Raised when the palette should be shown (e.g. Ctrl+Shift+P).</summary>
    public event Action? OpenRequested;

    /// <summary>Raised after a successful command execution or explicit close.</summary>
    public event Action? CloseRequested;

    /// <summary>
    /// Raised when <see cref="FilteredEntries"/> and/or <see cref="SelectedIndex"/>
    /// change due to <see cref="SetQuery"/>, <see cref="MoveUp"/>, <see cref="MoveDown"/>,
    /// or <see cref="Open"/>.
    /// </summary>
    public event Action? SelectionChanged;

    public CommandPaletteViewModel(ICommandRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));

        OpenPaletteCommand = ReactiveCommand.Create(() => Open());
        _registry.Register(new CommandDescriptor(
            "palette.open",
            "Open Command Palette",
            "Palette",
            new[] { "Ctrl+Shift+P" },
            OpenPaletteCommand));
    }

    /// <summary>Whether the palette overlay is currently visible.</summary>
    public bool IsOpen => _isOpen;

    /// <summary>Current search query. Empty when the palette has just been opened.</summary>
    public string Query => _query;

    /// <summary>Current filtered and ordered entry list.</summary>
    public IReadOnlyList<PaletteEntry> FilteredEntries => _filteredEntries;

    /// <summary>
    /// Index into <see cref="FilteredEntries"/> of the currently selected entry.
    /// -1 when no entry is selected (empty list or no available entries).
    /// </summary>
    public int SelectedIndex => _selectedIndex;

    /// <summary>
    /// The currently selected <see cref="PaletteEntry"/>, or null when nothing is selected.
    /// </summary>
    public PaletteEntry? SelectedEntry =>
        _selectedIndex >= 0 && _selectedIndex < _filteredEntries.Count
            ? _filteredEntries[_selectedIndex]
            : null;

    /// <summary>
    /// Exposes the registry so the View can execute commands through
    /// <see cref="ICommandRegistry.Execute(string)"/> without the ViewModel
    /// referencing Avalonia types.
    /// </summary>
    public ICommandRegistry Registry => _registry;

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

    /// <summary>
    /// Opens the palette: resets state and raises <see cref="OpenRequested"/>.
    /// </summary>
    public void Open()
    {
        Reset();
        _isOpen = true;
        OpenRequested?.Invoke();
    }

    /// <summary>
    /// Closes the palette and raises <see cref="CloseRequested"/>.
    /// </summary>
    public void Close()
    {
        _isOpen = false;
        CloseRequested?.Invoke();
    }

    /// <summary>
    /// Updates the query, re-filters entries, and resets selection to the first
    /// available entry. Raises <see cref="SelectionChanged"/>.
    /// </summary>
    public void SetQuery(string? query)
    {
        _query = query ?? string.Empty;
        _filteredEntries = Filter(_query);
        ResetSelection();
        SelectionChanged?.Invoke();
    }

    /// <summary>
    /// Moves selection to the previous available entry, wrapping to the last
    /// available entry when at the top. Raises <see cref="SelectionChanged"/>.
    /// </summary>
    public void MoveUp()
    {
        if (_filteredEntries.Count == 0) return;

        var availableIndices = GetAvailableIndices();
        if (availableIndices.Count == 0) return;

        if (_selectedIndex < 0)
        {
            _selectedIndex = availableIndices[^1];
        }
        else
        {
            var currentPos = availableIndices.IndexOf(_selectedIndex);
            if (currentPos <= 0)
                _selectedIndex = availableIndices[^1];
            else
                _selectedIndex = availableIndices[currentPos - 1];
        }

        SelectionChanged?.Invoke();
    }

    /// <summary>
    /// Moves selection to the next available entry, wrapping to the first
    /// available entry when at the bottom. Raises <see cref="SelectionChanged"/>.
    /// </summary>
    public void MoveDown()
    {
        if (_filteredEntries.Count == 0) return;

        var availableIndices = GetAvailableIndices();
        if (availableIndices.Count == 0) return;

        if (_selectedIndex < 0)
        {
            _selectedIndex = availableIndices[0];
        }
        else
        {
            var currentPos = availableIndices.IndexOf(_selectedIndex);
            if (currentPos < 0 || currentPos >= availableIndices.Count - 1)
                _selectedIndex = availableIndices[0];
            else
                _selectedIndex = availableIndices[currentPos + 1];
        }

        SelectionChanged?.Invoke();
    }

    /// <summary>
    /// Executes the selected available command exactly once through
    /// <see cref="ICommandRegistry.Execute(string)"/>. Raises <see cref="CloseRequested"/>
    /// after execution. Returns false when no command is selected or the selected
    /// entry is unavailable.
    /// </summary>
    public bool ExecuteSelected()
    {
        var entry = SelectedEntry;
        if (entry is null || !entry.IsAvailable)
            return false;

        _registry.Execute(entry.Id);
        Close();
        return true;
    }

    /// <summary>
    /// Resets to initial state: empty query, all entries, first available selected.
    /// </summary>
    public void Reset()
    {
        _query = string.Empty;
        _filteredEntries = GetAllEntries();
        ResetSelection();
    }

    private void ResetSelection()
    {
        _selectedIndex = -1;
        for (var i = 0; i < _filteredEntries.Count; i++)
        {
            if (_filteredEntries[i].IsAvailable)
            {
                _selectedIndex = i;
                break;
            }
        }
    }

    private List<int> GetAvailableIndices()
    {
        var result = new List<int>();
        for (var i = 0; i < _filteredEntries.Count; i++)
        {
            if (_filteredEntries[i].IsAvailable)
                result.Add(i);
        }
        return result;
    }
}
