using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Features.SourceControl.Domain;
using Zaide.Features.SourceControl.Contracts;
using Zaide.Features.SourceControl.Application;
using Zaide.Features.SourceControl.Presentation;
using Zaide.App.Composition;
using Zaide.App.Shell;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Presentation;

using Zaide.Tests;
using Zaide.Tests.App.Composition;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Infrastructure;
using Zaide.Features.Settings.Presentation;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Workspace.Infrastructure;
using Zaide.Features.Workspace.Presentation;
using Zaide.Tests.Features.Editor.Infrastructure;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Tests.Features.ProjectSystem;
using Zaide.Tests.Features.Debugging.Application;
using Zaide.Tests.Features.Debugging.Presentation;
using Zaide.Features.Terminal.Contracts;
using Zaide.Features.Terminal.Application;
using Zaide.Features.Terminal.Infrastructure;
using Zaide.Features.Terminal.Presentation;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Presentation;

namespace Zaide.Tests.Features.Settings.Infrastructure;

/// <summary>
/// Phase 8.2 M9a: window keybinding materialization. Seam-level tests for
/// neutral-gesture-to-Avalonia conversion (via UI-layer KeyBindingConverter),
/// generated-binding tracking, replacement without duplicates, preservation
/// of unrelated bindings, and Ctrl+Shift+H registry execution.
/// </summary>
public sealed class KeyBindingMaterializationTests
{
    static KeyBindingMaterializationTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private readonly TestLoggerProvider _loggerProvider;
    private readonly CommandRegistry _registry;
    private readonly Mock<ISettingsService> _settingsMock;

    public KeyBindingMaterializationTests()
    {
        _loggerProvider = new TestLoggerProvider();
        var loggerFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddProvider(_loggerProvider));
        var logger = loggerFactory.CreateLogger<CommandRegistry>();
        _registry = new CommandRegistry(logger);
        _settingsMock = new Mock<ISettingsService>();
        _settingsMock.SetupGet(x => x.Current).Returns(SettingsModel.Defaults);
        _settingsMock.SetupGet(x => x.WhenChanged).Returns(Observable.Empty<SettingsModel>());
    }

    /// <summary>
    /// Register all seven canonical commands using real owning ViewModels,
    /// mirroring the production composition root.
    /// </summary>
    private void RegisterAllViewModels()
    {
        var sp = new ServiceCollection()
            .AddSingleton<IFileService>(new global::Zaide.Tests.Features.Editor.Infrastructure.MockFileService())
            .AddSingleton<IEditorSessionFactory, EditorSessionFactory>()
            .AddSingleton<global::Zaide.Features.Workspace.Domain.Workspace>()
            .BuildServiceProvider();

        _ = new FileTreeViewModel(new FileTreeService(), CurrentThreadScheduler.Instance, _registry);
        var editorTabs = new EditorTabViewModel(sp.GetRequiredService<IEditorSessionFactory>(), sp.GetRequiredService<IFileService>(), sp.GetRequiredService<global::Zaide.Features.Workspace.Domain.Workspace>());
        _ = new MainWindowViewModel(
            new FileTreeViewModel(new FileTreeService(), CurrentThreadScheduler.Instance),
            editorTabs,
            new TerminalHost(new Mock<ITerminalSessionFactory>().Object),
            new AgentPanelHost(),
            new Mock<IAgentExecutionCoordinator>().Object,
            new AgentRouter(new MentionParser(new AgentPanelHost()), new AgentPanelHost(),
                new Mock<IAgentExecutionCoordinator>().Object),
            new TownhallViewModel(new TownhallState()),
            CreateSourceControlViewModel(),
            TestProblemsFactory.CreateWithWorkspace(sp.GetRequiredService<global::Zaide.Features.Workspace.Domain.Workspace>()),
            TestProjectWorkflowFactory.Create(registry: _registry),
            TestTestResultsFactory.Create(),
            TestDebugSessionFactory.Create(_registry),
            TestDebugPanelFactory.Create(),
            TestEditorBreakpointFactory.Create(editorTabs, _registry),
            sp.GetRequiredService<global::Zaide.Features.Workspace.Domain.Workspace>(),
            new Mock<IProjectContextService>(MockBehavior.Loose).Object, _registry);
    }

    private static SourceControlViewModel CreateSourceControlViewModel()
    {
        var git = new Mock<IGitRepositoryService>();
        git.Setup(g => g.Discover(It.IsAny<string>())).Returns(RepositoryDiscoveryResult.NotFound(""));
        git.Setup(g => g.ReadStatus(It.IsAny<string>())).Returns(new RepositoryStatusSnapshot());
        var orchestrator = new SourceControlSnapshotOrchestrator(git.Object);

        return new SourceControlViewModel(orchestrator, new global::Zaide.Features.Workspace.Domain.Workspace(),
            new Mock<IGitMutationService>().Object, git.Object, /* commandRegistry: */ null);
    }

    // ── Test 1: ParseToKeyGesture ────────────────────────────────────────

    [Theory]
    [InlineData("Ctrl+Oem3", Key.Oem3, KeyModifiers.Control)]
    [InlineData("Ctrl+J", Key.J, KeyModifiers.Control)]
    [InlineData("Ctrl+S", Key.S, KeyModifiers.Control)]
    [InlineData("Ctrl+O", Key.O, KeyModifiers.Control)]
    [InlineData("Ctrl+Shift+H", Key.H, KeyModifiers.Control | KeyModifiers.Shift)]
    public void ParseToKeyGesture_ConvertsCanonicalGestures(string normalized, Key expectedKey, KeyModifiers expectedModifiers)
    {
        var gesture = KeyBindingConverter.ParseToKeyGesture(normalized);

        Assert.Equal(expectedKey, gesture.Key);
        Assert.Equal(expectedModifiers, gesture.KeyModifiers);
    }

    // ── Test 2: TryCreateKeyBinding ──────────────────────────────────────

    [Fact]
    public void TryCreateKeyBinding_ConvertsResolvedBinding()
    {
        RegisterAllViewModels();

        var resolved = new ResolvedKeyBinding("Ctrl+S", "file.save");
        var descriptor = _registry.GetById("file.save");
        var binding = KeyBindingConverter.TryCreateKeyBinding(resolved, descriptor);

        Assert.NotNull(binding);
        Assert.NotNull(binding!.Gesture);
        Assert.Equal(Key.S, binding.Gesture!.Key);
        Assert.Equal(KeyModifiers.Control, binding.Gesture.KeyModifiers);
        Assert.NotNull(binding.Command);
    }

    [Fact]
    public void TryCreateKeyBinding_ReturnsNullForNullDescriptor()
    {
        var resolved = new ResolvedKeyBinding("Ctrl+X", "unknown.command");
        var binding = KeyBindingConverter.TryCreateKeyBinding(resolved, null);

        Assert.Null(binding);
    }

    // ── Test 3: Full materialization via registry + converter ────────────

    [Fact]
    public void ResolveAndConvert_ProducesBindingsForAllResolvedGestures()
    {
        RegisterAllViewModels();

        var resolved = _registry.ResolveKeyBindings(_settingsMock.Object);
        var bindings = new List<KeyBinding>();
        foreach (var r in resolved)
        {
            var kb = KeyBindingConverter.TryCreateKeyBinding(r, _registry.GetById(r.CommandId));
            if (kb is not null)
                bindings.Add(kb);
        }

        // Expect defaults including debug execution controls (F5/F9/F10/F11/Shift+F5/Shift+F11).
        Assert.Equal(14, bindings.Count);
    }

    [Fact]
    public void ResolveAndConvert_Oem3MapsToOem3WithControl()
    {
        RegisterAllViewModels();

        var resolved = _registry.ResolveKeyBindings(_settingsMock.Object);
        var bindings = resolved
            .Select(r => KeyBindingConverter.TryCreateKeyBinding(r, _registry.GetById(r.CommandId)))
            .Where(kb => kb is not null)
            .Cast<KeyBinding>()
            .ToList();

        var oem3Binding = bindings.FirstOrDefault(b =>
            b.Gesture?.Key == Key.Oem3 && b.Gesture?.KeyModifiers == KeyModifiers.Control);
        Assert.NotNull(oem3Binding);
    }

    [Fact]
    public void ResolveAndConvert_CtrlShiftH_HasCorrectGestureAndCommand()
    {
        RegisterAllViewModels();

        var resolved = _registry.ResolveKeyBindings(_settingsMock.Object);
        var bindings = resolved
            .Select(r => KeyBindingConverter.TryCreateKeyBinding(r, _registry.GetById(r.CommandId)))
            .Where(kb => kb is not null)
            .Cast<KeyBinding>()
            .ToList();

        var toggleHidden = bindings.FirstOrDefault(b =>
            b.Gesture?.Key == Key.H &&
            b.Gesture?.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift));

        Assert.NotNull(toggleHidden);
        Assert.NotNull(toggleHidden!.Command);
        Assert.True(toggleHidden.Command.CanExecute(null));
    }

    // ── Test 4: Generated binding tracking and replacement ───────────────

    [Fact]
    public void ResolveAndConvert_ReturnsNewInstancesOnEachCall()
    {
        RegisterAllViewModels();

        var resolve = () => _registry.ResolveKeyBindings(_settingsMock.Object)
            .Select(r => KeyBindingConverter.TryCreateKeyBinding(r, _registry.GetById(r.CommandId)))
            .Where(kb => kb is not null)
            .Cast<KeyBinding>()
            .ToList();

        var first = resolve();
        var second = resolve();

        Assert.Equal(first.Count, second.Count);
        for (int i = 0; i < first.Count; i++)
            Assert.NotSame(first[i], second[i]);
    }

    // ── Test 5: Replacement without duplicates ───────────────────────────

    [Fact]
    public void ReplaceBindings_RemovesOldAndAddsNewWithoutDuplicates()
    {
        RegisterAllViewModels();

        // Simulate the window's KeyBindings collection and tracking list
        var keyBindings = new List<KeyBinding>();
        var tracked = new List<KeyBinding>();

        // Helper to materialize bindings (mirrors MainWindow.MaterializeRegistryBindings)
        void Materialize()
        {
            // Remove old
            foreach (var kb in tracked)
                keyBindings.Remove(kb);
            tracked.Clear();

            // Resolve and convert
            var resolved = _registry.ResolveKeyBindings(_settingsMock.Object);
            foreach (var r in resolved)
            {
                var kb = KeyBindingConverter.TryCreateKeyBinding(r, _registry.GetById(r.CommandId));
                if (kb is null) continue;
                keyBindings.Add(kb);
                tracked.Add(kb);
            }
        }

        // First materialization
        Materialize();
        Assert.Equal(14, keyBindings.Count);

        // Second materialization (simulating a refresh)
        Materialize();
        Assert.Equal(14, keyBindings.Count);
        Assert.Equal(14, tracked.Count);
    }

    // ── Test 6: Preservation of unrelated/view-local bindings ────────────

    [Fact]
    public void ReplaceBindings_PreservesUnrelatedBindings()
    {
        RegisterAllViewModels();

        var keyBindings = new List<KeyBinding>();
        var tracked = new List<KeyBinding>();

        // Add unrelated binding (e.g., a view-local Enter handler)
        var unrelated = new KeyBinding
        {
            Gesture = new KeyGesture(Key.Enter),
            Command = new AlwaysEnabledCommand()
        };
        keyBindings.Add(unrelated);

        void Materialize()
        {
            foreach (var kb in tracked)
                keyBindings.Remove(kb);
            tracked.Clear();

            var resolved = _registry.ResolveKeyBindings(_settingsMock.Object);
            foreach (var r in resolved)
            {
                var kb = KeyBindingConverter.TryCreateKeyBinding(r, _registry.GetById(r.CommandId));
                if (kb is null) continue;
                keyBindings.Add(kb);
                tracked.Add(kb);
            }
        }

        Materialize();
        Assert.Equal(15, keyBindings.Count); // 1 unrelated + 14 registry
        Assert.Contains(unrelated, keyBindings);

        // Replace registry bindings
        Materialize();
        Assert.Contains(unrelated, keyBindings);
        Assert.Equal(15, keyBindings.Count);
    }

    // ── Test 7: Empty registry produces no bindings ──────────────────────

    [Fact]
    public void ResolveAndConvert_EmptyRegistry_ReturnsEmptyList()
    {
        // No commands registered
        var resolved = _registry.ResolveKeyBindings(_settingsMock.Object);
        Assert.Empty(resolved);

        var bindings = resolved
            .Select(r => KeyBindingConverter.TryCreateKeyBinding(r, _registry.GetById(r.CommandId)))
            .Where(kb => kb is not null)
            .ToList();

        Assert.Empty(bindings);
    }

    // ── Test 8: Ctrl+Shift+H executes through registry ──────────────────

    [Fact]
    public void Execute_CtrlShiftH_TogglesHiddenFiles()
    {
        RegisterAllViewModels();

        var result = _registry.Execute("explorer.toggleHiddenFiles");

        Assert.True(result);
    }

    /// <summary>
    /// Simple ICommand stub that always can execute and does nothing.
    /// </summary>
    private sealed class AlwaysEnabledCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { }
    }
}
