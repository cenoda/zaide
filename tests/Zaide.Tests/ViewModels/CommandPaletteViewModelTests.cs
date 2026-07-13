using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Xunit;
using Zaide.Services;
using Zaide.ViewModels;

namespace Zaide.Tests.ViewModels;

/// <summary>
/// Phase 9 M1: Tests for <see cref="CommandPaletteViewModel"/> — the UI-independent
/// palette query/presentation seam. All tests verify behavior without Avalonia controls.
/// </summary>
public sealed class CommandPaletteViewModelTests
{
    private static ICommandRegistry CreateRegistry()
    {
        return CommandRegistryFactory.Create();
    }

    private static CommandDescriptor CreateDescriptor(
        string id,
        string displayName,
        string category,
        ICommand? command = null)
    {
        return new CommandDescriptor(
            id, displayName, category,
            Array.Empty<string>(),
            command ?? new AlwaysEnabledCommandStub());
    }

    // ── Ordering ─────────────────────────────────────────────────────────

    [Fact]
    public void GetAllEntries_OrderedByCategoryThenDisplayNameThenId()
    {
        var registry = CreateRegistry();
        // Deliberately register in non-alphabetical order.
        registry.Register(CreateDescriptor("test.z", "Zulu Command", "Zulu"));
        registry.Register(CreateDescriptor("test.beta", "Beta Command", "Beta"));
        registry.Register(CreateDescriptor("test.alpha", "Alpha Command", "Alpha"));
        registry.Register(CreateDescriptor("test.gamma", "Gamma Command", "Alpha"));
        registry.Register(CreateDescriptor("test.tie", "Alpha Command", "Alpha"));

        var vm = new CommandPaletteViewModel(registry);

        // palette.open is auto-registered: "Open Command Palette" / "Palette".
        // Expected order:
        //   1. Alpha (Alpha Command / test.alpha)
        //   2. Alpha (Alpha Command / test.tie)  — same display, ordered by ID
        //   3. Alpha (Gamma Command / test.gamma)
        //   4. Beta  (Beta Command  / test.beta)
        //   5. Palette (Open Command Palette / palette.open)
        //   6. Zulu  (Zulu Command  / test.z)
        var entries = vm.GetAllEntries();

        Assert.Equal(6, entries.Count);
        Assert.Equal("test.alpha", entries[0].Id);
        Assert.Equal("test.tie", entries[1].Id);
        Assert.Equal("test.gamma", entries[2].Id);
        Assert.Equal("test.beta", entries[3].Id);
        Assert.Equal("palette.open", entries[4].Id);
        Assert.Equal("test.z", entries[5].Id);
    }

    [Fact]
    public void GetAllEntries_OrderingIsCaseInsensitive()
    {
        var registry = CreateRegistry();
        registry.Register(CreateDescriptor("test.a", "Alpha Command", "alpha"));
        registry.Register(CreateDescriptor("test.b", "Beta Command", "BETA"));

        var vm = new CommandPaletteViewModel(registry);

        // palette.open (Palette) sorts after both alpha and BETA
        // because 'p' > 'a' and 'p' > 'b' even case-insensitively.
        var entries = vm.GetAllEntries();

        var ids = entries.Select(e => e.Id).ToList();
        var alphaIndex = ids.IndexOf("test.a");
        var betaIndex = ids.IndexOf("test.b");
        var paletteIndex = ids.IndexOf("palette.open");

        Assert.True(alphaIndex < betaIndex);
        Assert.True(betaIndex < paletteIndex);
    }

    // ── Filtering ────────────────────────────────────────────────────────

    [Fact]
    public void Filter_EmptyQuery_ReturnsAllEntries()
    {
        var registry = CreateRegistry();
        registry.Register(CreateDescriptor("cmd.a", "Alpha Command", "General"));
        registry.Register(CreateDescriptor("cmd.b", "Beta Command", "General"));

        var vm = new CommandPaletteViewModel(registry);
        var all = vm.GetAllEntries();
        var filtered = vm.Filter("");

        Assert.Equal(all.Count, filtered.Count);
    }

    [Fact]
    public void Filter_NullQuery_ReturnsAllEntries()
    {
        var registry = CreateRegistry();
        registry.Register(CreateDescriptor("cmd.a", "Alpha Command", "General"));

        var vm = new CommandPaletteViewModel(registry);
        var all = vm.GetAllEntries();
        var filtered = vm.Filter(null);

        Assert.Equal(all.Count, filtered.Count);
    }

    [Fact]
    public void Filter_CaseInsensitiveSubstring_ReturnsMatchingEntries()
    {
        var registry = CreateRegistry();
        registry.Register(CreateDescriptor("cmd.find", "Find Something", "Editor"));
        registry.Register(CreateDescriptor("cmd.refind", "Refind Something", "Editor"));
        registry.Register(CreateDescriptor("cmd.save", "Save File", "File"));

        var vm = new CommandPaletteViewModel(registry);

        var result = vm.Filter("find");

        Assert.Contains(result, e => e.Id == "cmd.find");
        Assert.Contains(result, e => e.Id == "cmd.refind");
        Assert.DoesNotContain(result, e => e.Id == "cmd.save");
    }

    [Fact]
    public void Filter_MatchesDifferentCase()
    {
        var registry = CreateRegistry();
        registry.Register(CreateDescriptor("cmd.find", "Find Something", "Editor"));

        var vm = new CommandPaletteViewModel(registry);

        var upper = vm.Filter("FIND");
        Assert.Contains(upper, e => e.Id == "cmd.find");

        var lower = vm.Filter("find");
        Assert.Contains(lower, e => e.Id == "cmd.find");

        var mixed = vm.Filter("fInD");
        Assert.Contains(mixed, e => e.Id == "cmd.find");
    }

    [Fact]
    public void Filter_MatchesAgainstDisplayNameOnly_NotId()
    {
        var registry = CreateRegistry();
        // "find" appears in the ID but not in the display name.
        registry.Register(CreateDescriptor("find.stuff", "Search Results", "Editor"));
        // "Find" appears in the display name.
        registry.Register(CreateDescriptor("cmd.find", "Find Something", "Editor"));

        var vm = new CommandPaletteViewModel(registry);

        var result = vm.Filter("find");

        Assert.Contains(result, e => e.Id == "cmd.find");
        Assert.DoesNotContain(result, e => e.Id == "find.stuff");
    }

    [Fact]
    public void Filter_NoMatch_ReturnsEmptyList()
    {
        var registry = CreateRegistry();
        registry.Register(CreateDescriptor("cmd.a", "Alpha Command", "General"));

        var vm = new CommandPaletteViewModel(registry);

        var result = vm.Filter("zzzznothing");
        Assert.Empty(result);
    }

    [Fact]
    public void Filter_ResultStillRespectsOrdering()
    {
        var registry = CreateRegistry();
        registry.Register(CreateDescriptor("cmd.z", "Zebra Find", "Editor"));
        registry.Register(CreateDescriptor("cmd.a", "Alpha Find", "Editor"));

        var vm = new CommandPaletteViewModel(registry);

        var result = vm.Filter("find");

        Assert.Equal(2, result.Count);
        Assert.Equal("cmd.a", result[0].Id);
        Assert.Equal("cmd.z", result[1].Id);
    }

    // ── Availability ─────────────────────────────────────────────────────

    [Fact]
    public void UnavailableCommand_IsIncludedButMarkedUnavailable()
    {
        var registry = CreateRegistry();
        registry.Register(new CommandDescriptor(
            "cmd.disabled",
            "Disabled Command",
            "Test",
            Array.Empty<string>(),
            new AlwaysDisabledCommandStub()));

        var vm = new CommandPaletteViewModel(registry);

        var entry = vm.GetAllEntries().FirstOrDefault(e => e.Id == "cmd.disabled");
        Assert.NotNull(entry);
        Assert.False(entry!.IsAvailable);
    }

    [Fact]
    public void AvailableCommand_IsMarkedAvailable()
    {
        var registry = CreateRegistry();
        registry.Register(new CommandDescriptor(
            "cmd.enabled",
            "Enabled Command",
            "Test",
            Array.Empty<string>(),
            new AlwaysEnabledCommandStub()));

        var vm = new CommandPaletteViewModel(registry);

        // palette.open is also always available
        var paletteEntry = vm.GetAllEntries().FirstOrDefault(e => e.Id == "palette.open");
        Assert.NotNull(paletteEntry);
        Assert.True(paletteEntry!.IsAvailable);

        var enabledEntry = vm.GetAllEntries().FirstOrDefault(e => e.Id == "cmd.enabled");
        Assert.NotNull(enabledEntry);
        Assert.True(enabledEntry!.IsAvailable);
    }

    [Fact]
    public void GetAllEntries_DoesNotContainUnknownIds()
    {
        // Only registered descriptors become palette entries.
        // An ID that was never registered must not appear, even if someone
        // later tries to execute it via ICommandRegistry.Execute().
        var registry = CreateRegistry();
        registry.Register(CreateDescriptor("cmd.known", "Known Command", "Test"));
        var vm = new CommandPaletteViewModel(registry);

        var entries = vm.GetAllEntries();

        Assert.Contains(entries, e => e.Id == "cmd.known");
        Assert.DoesNotContain(entries, e => e.Id == "cmd.unknown");
    }

    [Fact]
    public void PaletteItselfAppearsInEntries()
    {
        var registry = CreateRegistry();
        var vm = new CommandPaletteViewModel(registry);

        var entry = vm.GetAllEntries().FirstOrDefault(e => e.Id == "palette.open");
        Assert.NotNull(entry);
        Assert.Equal("Open Command Palette", entry!.DisplayName);
        Assert.Equal("Palette", entry.Category);
        Assert.True(entry.IsAvailable);
    }

    // ── Stubs ────────────────────────────────────────────────────────────

    private sealed class AlwaysEnabledCommandStub : ICommand
    {
        public int ExecutionCount { get; private set; }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => ExecutionCount++;
    }

    private sealed class AlwaysDisabledCommandStub : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => false;
        public void Execute(object? parameter) { }
    }

    // ── M2: Open / Close lifecycle ───────────────────────────────────────

    [Fact]
    public void Open_SetsIsOpenAndResetsState()
    {
        var registry = CreateRegistry();
        registry.Register(CreateDescriptor("cmd.a", "Alpha", "Test"));
        var vm = new CommandPaletteViewModel(registry);

        vm.SetQuery("something");
        vm.Open();

        Assert.True(vm.IsOpen);
        Assert.Equal(string.Empty, vm.Query);
        Assert.True(vm.FilteredEntries.Count > 0);
    }

    [Fact]
    public void Open_RaisesOpenRequestedEvent()
    {
        var registry = CreateRegistry();
        var vm = new CommandPaletteViewModel(registry);
        var raised = false;
        vm.OpenRequested += () => raised = true;

        vm.Open();

        Assert.True(raised);
    }

    [Fact]
    public void Close_SetsIsOpenFalse_RaisesCloseRequested()
    {
        var registry = CreateRegistry();
        var vm = new CommandPaletteViewModel(registry);
        vm.Open();

        var raised = false;
        vm.CloseRequested += () => raised = true;
        vm.Close();

        Assert.False(vm.IsOpen);
        Assert.True(raised);
    }

    // ── M2: Selection state ──────────────────────────────────────────────

    [Fact]
    public void InitialSelection_FirstAvailableEntry()
    {
        var registry = CreateRegistry();
        registry.Register(CreateDescriptor("cmd.a", "Alpha", "Test"));
        registry.Register(CreateDescriptor("cmd.b", "Beta", "Test"));
        var vm = new CommandPaletteViewModel(registry);

        vm.Open();

        // palette.open is auto-registered and available; "Alpha" sorts before "Beta"
        // and both sort before "Open Command Palette" (category order: Test < Palette? No: "Palette" < "Test")
        // Actually: "Palette" < "Test" alphabetically. So palette.open comes first.
        Assert.Equal(0, vm.SelectedIndex);
        Assert.NotNull(vm.SelectedEntry);
        Assert.True(vm.SelectedEntry!.IsAvailable);
    }

    [Fact]
    public void InitialSelection_NoAvailableEntries_SelectedIndexIsNegativeOne()
    {
        var registry = CreateRegistry();
        registry.Register(new CommandDescriptor(
            "cmd.disabled", "Disabled", "Test",
            Array.Empty<string>(), new AlwaysDisabledCommandStub()));
        // palette.open is always available, so we need a VM that doesn't auto-register.
        // Instead, verify that when all filtered entries are unavailable, SelectedIndex is -1.
        var vm = new CommandPaletteViewModel(registry);
        vm.Open();

        // Filter to only show "Disabled" — palette.open won't match
        vm.SetQuery("Disabled");

        Assert.Single(vm.FilteredEntries);
        Assert.False(vm.FilteredEntries[0].IsAvailable);
        Assert.Equal(-1, vm.SelectedIndex);
        Assert.Null(vm.SelectedEntry);
    }

    [Fact]
    public void EmptyFilteredEntries_SelectedIndexIsNegativeOne()
    {
        var registry = CreateRegistry();
        registry.Register(CreateDescriptor("cmd.a", "Alpha", "Test"));
        var vm = new CommandPaletteViewModel(registry);
        vm.Open();

        vm.SetQuery("zzzznothing");

        Assert.Empty(vm.FilteredEntries);
        Assert.Equal(-1, vm.SelectedIndex);
        Assert.Null(vm.SelectedEntry);
    }

    // ── M2: Navigation ───────────────────────────────────────────────────

    [Fact]
    public void MoveDown_AdvancesToNextAvailable()
    {
        var registry = CreateRegistry();
        registry.Register(CreateDescriptor("cmd.a", "Alpha", "Test"));
        registry.Register(CreateDescriptor("cmd.b", "Beta", "Test"));
        var vm = new CommandPaletteViewModel(registry);
        vm.Open();

        var initialIndex = vm.SelectedIndex;
        vm.MoveDown();

        Assert.True(vm.SelectedIndex > initialIndex || vm.SelectedIndex == 0);
    }

    [Fact]
    public void MoveDown_WrapsFromLastToFirst()
    {
        var registry = CreateRegistry();
        registry.Register(CreateDescriptor("cmd.a", "Alpha", "Test"));
        var vm = new CommandPaletteViewModel(registry);
        vm.Open();

        // Navigate to the last available entry
        var entries = vm.FilteredEntries;
        var availableCount = entries.Count(e => e.IsAvailable);
        for (var i = 0; i < availableCount - 1; i++)
            vm.MoveDown();

        var lastIndex = vm.SelectedIndex;
        vm.MoveDown(); // should wrap

        // After wrapping, should be at the first available entry
        var firstAvailableIndex = -1;
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].IsAvailable) { firstAvailableIndex = i; break; }
        }
        Assert.Equal(firstAvailableIndex, vm.SelectedIndex);
        Assert.NotEqual(lastIndex, vm.SelectedIndex);
    }

    [Fact]
    public void MoveUp_WrapsFromFirstToLast()
    {
        var registry = CreateRegistry();
        registry.Register(CreateDescriptor("cmd.a", "Alpha", "Test"));
        var vm = new CommandPaletteViewModel(registry);
        vm.Open();

        // At first available entry
        var firstIndex = vm.SelectedIndex;
        var entries = vm.FilteredEntries;
        var availableIndices = Enumerable.Range(0, entries.Count)
            .Where(i => entries[i].IsAvailable).ToList();
        var lastAvailableIndex = availableIndices[^1];

        vm.MoveUp(); // should wrap to last

        Assert.Equal(lastAvailableIndex, vm.SelectedIndex);
    }

    [Fact]
    public void MoveDown_SkipsUnavailableEntries()
    {
        var registry = CreateRegistry();
        registry.Register(CreateDescriptor("cmd.a", "Alpha", "Test"));
        registry.Register(new CommandDescriptor(
            "cmd.disabled", "Beta Disabled", "Test",
            Array.Empty<string>(), new AlwaysDisabledCommandStub()));
        registry.Register(CreateDescriptor("cmd.c", "Charlie", "Test"));
        var vm = new CommandPaletteViewModel(registry);
        vm.Open();

        // Filter to show all three "Test" entries + palette.open
        // Navigate from first available past the disabled one
        var entries = vm.FilteredEntries;
        var startIndex = vm.SelectedIndex;

        // Move down until we pass the disabled entry
        var visitedIndices = new List<int> { startIndex };
        for (var i = 0; i < entries.Count; i++)
        {
            vm.MoveDown();
            if (vm.SelectedIndex == startIndex) break; // wrapped
            visitedIndices.Add(vm.SelectedIndex);
        }

        // The disabled entry index should never appear in visited indices
        var disabledIndex = entries.ToList().FindIndex(e => e.Id == "cmd.disabled");
        Assert.DoesNotContain(disabledIndex, visitedIndices);
    }

    [Fact]
    public void MoveUp_SkipsUnavailableEntries()
    {
        var registry = CreateRegistry();
        registry.Register(CreateDescriptor("cmd.a", "Alpha", "Test"));
        registry.Register(new CommandDescriptor(
            "cmd.disabled", "Zeta Disabled", "Test",
            Array.Empty<string>(), new AlwaysDisabledCommandStub()));
        registry.Register(CreateDescriptor("cmd.c", "Charlie", "Test"));
        var vm = new CommandPaletteViewModel(registry);
        vm.Open();

        var entries = vm.FilteredEntries;
        var disabledIndex = entries.ToList().FindIndex(e => e.Id == "cmd.disabled");

        // Navigate through all entries via MoveUp
        var visitedIndices = new List<int> { vm.SelectedIndex };
        for (var i = 0; i < entries.Count; i++)
        {
            vm.MoveUp();
            if (vm.SelectedIndex == visitedIndices[0]) break;
            visitedIndices.Add(vm.SelectedIndex);
        }

        Assert.DoesNotContain(disabledIndex, visitedIndices);
    }

    [Fact]
    public void Navigation_EmptyList_DoesNotThrow()
    {
        var registry = CreateRegistry();
        registry.Register(CreateDescriptor("cmd.a", "Alpha", "Test"));
        var vm = new CommandPaletteViewModel(registry);
        vm.Open();

        vm.SetQuery("zzzznothing");

        vm.MoveUp();
        vm.MoveDown();

        Assert.Equal(-1, vm.SelectedIndex);
    }

    [Fact]
    public void Navigation_AllUnavailable_DoesNotThrow()
    {
        var registry = CreateRegistry();
        registry.Register(new CommandDescriptor(
            "cmd.disabled", "Disabled", "Test",
            Array.Empty<string>(), new AlwaysDisabledCommandStub()));
        var vm = new CommandPaletteViewModel(registry);
        vm.Open();
        vm.SetQuery("Disabled");

        vm.MoveUp();
        vm.MoveDown();

        Assert.Equal(-1, vm.SelectedIndex);
    }

    // ── M2: Query / Filter update ────────────────────────────────────────

    [Fact]
    public void SetQuery_UpdatesFilteredEntries()
    {
        var registry = CreateRegistry();
        registry.Register(CreateDescriptor("cmd.find", "Find", "Editor"));
        registry.Register(CreateDescriptor("cmd.save", "Save", "File"));
        var vm = new CommandPaletteViewModel(registry);
        vm.Open();

        vm.SetQuery("find");

        Assert.Contains(vm.FilteredEntries, e => e.Id == "cmd.find");
        Assert.DoesNotContain(vm.FilteredEntries, e => e.Id == "cmd.save");
    }

    [Fact]
    public void SetQuery_RaisesSelectionChanged()
    {
        var registry = CreateRegistry();
        registry.Register(CreateDescriptor("cmd.a", "Alpha", "Test"));
        var vm = new CommandPaletteViewModel(registry);
        vm.Open();

        var raised = false;
        vm.SelectionChanged += () => raised = true;
        vm.SetQuery("Alpha");

        Assert.True(raised);
    }

    [Fact]
    public void SetQuery_ResetsSelectionToFirstAvailable()
    {
        var registry = CreateRegistry();
        registry.Register(CreateDescriptor("cmd.a", "Alpha", "Test"));
        registry.Register(CreateDescriptor("cmd.b", "Beta", "Test"));
        var vm = new CommandPaletteViewModel(registry);
        vm.Open();

        vm.MoveDown();
        var movedIndex = vm.SelectedIndex;

        vm.SetQuery("Alpha");

        // After filtering, selection should reset to first available match
        Assert.True(vm.SelectedIndex >= 0);
        Assert.True(vm.SelectedIndex != movedIndex || vm.FilteredEntries.Count == 1);
    }

    // ── M2: Execution ────────────────────────────────────────────────────

    [Fact]
    public void ExecuteSelected_ExecutesThroughRegistry_ExactlyOnce()
    {
        var registry = CreateRegistry();
        var cmd = new AlwaysEnabledCommandStub();
        registry.Register(new CommandDescriptor(
            "cmd.test", "Test Command", "Test",
            Array.Empty<string>(), cmd));
        var vm = new CommandPaletteViewModel(registry);
        vm.Open();

        // Navigate to "Test Command"
        vm.SetQuery("Test Command");
        Assert.Equal(0, vm.SelectedIndex);
        Assert.Equal("cmd.test", vm.SelectedEntry!.Id);

        var result = vm.ExecuteSelected();

        Assert.True(result);
        Assert.Equal(1, cmd.ExecutionCount);
    }

    [Fact]
    public void ExecuteSelected_DismissesPalette()
    {
        var registry = CreateRegistry();
        registry.Register(CreateDescriptor("cmd.test", "Test Command", "Test"));
        var vm = new CommandPaletteViewModel(registry);
        vm.Open();

        vm.SetQuery("Test Command");
        vm.ExecuteSelected();

        Assert.False(vm.IsOpen);
    }

    [Fact]
    public void ExecuteSelected_UnavailableEntry_ReturnsFalse_DoesNotDismiss()
    {
        var registry = CreateRegistry();
        registry.Register(new CommandDescriptor(
            "cmd.disabled", "Disabled", "Test",
            Array.Empty<string>(), new AlwaysDisabledCommandStub()));
        var vm = new CommandPaletteViewModel(registry);
        vm.Open();
        vm.SetQuery("Disabled");

        Assert.Equal(-1, vm.SelectedIndex);
        var result = vm.ExecuteSelected();

        Assert.False(result);
        Assert.True(vm.IsOpen); // not dismissed
    }

    [Fact]
    public void ExecuteSelected_NoSelection_ReturnsFalse()
    {
        var registry = CreateRegistry();
        registry.Register(CreateDescriptor("cmd.a", "Alpha", "Test"));
        var vm = new CommandPaletteViewModel(registry);
        vm.Open();

        vm.SetQuery("zzzznothing");
        var result = vm.ExecuteSelected();

        Assert.False(result);
    }

    [Fact]
    public void OpenPaletteCommand_IsRegisteredAndOpensPalette()
    {
        var registry = CreateRegistry();
        var vm = new CommandPaletteViewModel(registry);

        var raised = false;
        vm.OpenRequested += () => raised = true;

        vm.OpenPaletteCommand.Execute(null);

        Assert.True(raised);
        Assert.True(vm.IsOpen);
    }
}
