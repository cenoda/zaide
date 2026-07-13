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
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { }
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
}
