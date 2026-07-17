using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using ReactiveUI;
using ReactiveUI.Avalonia;
using ReactiveUI.Builder;
using Splat;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.ViewModels;
using Zaide.Views;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Presentation;
using Zaide.Features.Settings.Infrastructure;

namespace Zaide.Tests.Features.Settings.Presentation;

public sealed class SettingsPanelViewTests
{
    static SettingsPanelViewTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
        Locator.CurrentMutable.Register(() => new AvaloniaActivationForViewFetcher(), typeof(IActivationForViewFetcher));
        EnsureApplication();
    }

    [Fact]
    public void PanelConstructs_WithoutThrowing()
    {
        using var settings = new TestSettingsService();
        using var vm = new SettingsViewModel(settings, new TestSecretStore());
        using var panel = new SettingsPanelView(vm);

        Assert.NotNull(panel);
        Assert.NotNull(panel.Content);
    }

    [Fact]
    public void PanelContent_IncludesVerticalScrollViewer()
    {
        using var settings = new TestSettingsService();
        using var vm = new SettingsViewModel(settings, new TestSecretStore());
        using var panel = new SettingsPanelView(vm);

        var scrollViewer = GetAllDescendants(panel).OfType<ScrollViewer>().Single();
        Assert.Equal(Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, scrollViewer.VerticalScrollBarVisibility);
        Assert.Equal(Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, scrollViewer.HorizontalScrollBarVisibility);
        Assert.IsType<StackPanel>(scrollViewer.Content);
    }

    [Fact]
    public void LabelledSections_EditorTerminalLlm_ArePresent()
    {
        using var settings = new TestSettingsService();
        using var vm = new SettingsViewModel(settings, new TestSecretStore());
        using var panel = new SettingsPanelView(vm);

        var textBlocks = GetAllDescendants(panel).OfType<TextBlock>().ToList();
        var sectionHeaders = textBlocks
            .Where(tb => tb.Text is "Editor" or "Terminal" or "LLM")
            .Select(tb => tb.Text)
            .OrderBy(x => x)
            .ToList();

        Assert.Equal(3, sectionHeaders.Count);
        Assert.Equal("Editor", sectionHeaders[0]);
        Assert.Equal("LLM", sectionHeaders[1]);
        Assert.Equal("Terminal", sectionHeaders[2]);
    }

    [Fact]
    public void AllEditorControls_AreConnectedToViewModel()
    {
        using var settings = new TestSettingsService();
        using var vm = new SettingsViewModel(settings, new TestSecretStore());
        using var panel = new SettingsPanelView(vm);

        var textBoxes = GetAllDescendants(panel).OfType<TextBox>().ToList();
        var fontPickers = GetAllDescendants(panel).OfType<SettingsFontPicker>().ToList();
        var checkBoxes = GetAllDescendants(panel).OfType<CheckBox>().ToList();

        // Font families use pickers; sizes and LLM fields remain text boxes.
        Assert.Equal(3, fontPickers.Count);
        Assert.True(textBoxes.Count >= 6, $"Expected >= 6 TextBox controls, found {textBoxes.Count}");
        Assert.True(checkBoxes.Count >= 4, $"Expected >= 4 CheckBox controls, found {checkBoxes.Count}");
    }

    [Fact]
    public void FontPickers_StartClosed_WithScrollableInstalledFontsInPopup()
    {
        using var settings = new TestSettingsService();
        using var vm = new SettingsViewModel(settings, new TestSecretStore());
        using var panel = new SettingsPanelView(vm);

        var fontPickers = GetAllDescendants(panel).OfType<SettingsFontPicker>().ToList();
        Assert.Equal(3, fontPickers.Count);

        foreach (var picker in fontPickers)
        {
            Assert.False(picker.IsDropDownOpen);

            var popup = GetAllDescendants(picker).OfType<Popup>().Single();
            popup.IsOpen = true;

            var listBox = GetAllDescendants(picker).OfType<ListBox>().Single();
            var items = listBox.ItemsSource as System.Collections.IEnumerable;
            Assert.NotNull(items);
            Assert.True(items!.Cast<object>().Count() > 8);
            Assert.Equal(SettingsFontPicker.DefaultMaxHeight, listBox.MaxHeight);
        }
    }

    [Fact]
    public void FontPickerSelection_UpdatesCandidateAndResyncsOnDiscard()
    {
        using var settings = new TestSettingsService();
        using var vm = new SettingsViewModel(settings, new TestSecretStore());
        using var panel = new SettingsPanelView(vm);

        var codePicker = GetAllDescendants(panel).OfType<SettingsFontPicker>().First();
        var listBox = GetAllDescendants(codePicker).OfType<ListBox>().Single();
        var entries = InstalledFontCatalog.BuildEntries(vm.Candidate.Editor.CodeFontFamily);
        var target = entries.First(entry =>
            entry.IsAvailable
            && !entry.Name.Equals(
                InstalledFontCatalog.ExtractPrimaryFamilyName(vm.Candidate.Editor.CodeFontFamily),
                System.StringComparison.OrdinalIgnoreCase));

        listBox.SelectedItem = target;
        listBox.RaiseEvent(new Avalonia.Input.KeyEventArgs
        {
            RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
            Source = listBox,
            Key = Avalonia.Input.Key.Enter,
        });

        Assert.Equal(target.Name, vm.Candidate.Editor.CodeFontFamily);

        vm.Discard();
        var primary = InstalledFontCatalog.ExtractPrimaryFamilyName(vm.Candidate.Editor.CodeFontFamily);
        var selected = Assert.IsType<FontPickerEntry>(listBox.SelectedItem);
        Assert.Equal(primary, selected.Name);
    }

    [Fact]
    public void Dispose_CanBeCalledTwice_WithoutCrash()
    {
        using var settings = new TestSettingsService();
        using var vm = new SettingsViewModel(settings, new TestSecretStore());
        var panel = new SettingsPanelView(vm);

        panel.Dispose();
        panel.Dispose(); // second call should be a no-op
    }

    [Fact]
    public void ApiKeyControl_UsesPasswordChar()
    {
        using var settings = new TestSettingsService();
        using var vm = new SettingsViewModel(settings, new TestSecretStore());
        vm.ApiKey = "test-secret";
        using var panel = new SettingsPanelView(vm);

        var apiKeyBox = GetAllDescendants(panel)
            .OfType<TextBox>()
            .FirstOrDefault(tb => tb.PasswordChar == '•');

        Assert.NotNull(apiKeyBox);
        Assert.Equal("test-secret", apiKeyBox.Text);
        Assert.Equal('•', apiKeyBox.PasswordChar);
    }

    [Fact]
    public void ApplyDiscard_UpdatesControlsCorrectly()
    {
        using var settings = new TestSettingsService();
        using var vm = new SettingsViewModel(settings, new TestSecretStore());
        using var panel = new SettingsPanelView(vm);

        // Update via ViewModel; the panel SyncFields callback should reflect it
        vm.SetModel("edited-model");

        var textBoxes = GetAllDescendants(panel).OfType<TextBox>().ToList();
        var modelBox = textBoxes.FirstOrDefault(tb => tb.PlaceholderText == "Model");
        Assert.NotNull(modelBox);
        Assert.Equal("edited-model", modelBox.Text);
        Assert.Equal("edited-model", vm.Candidate.Llm.Model);

        // Discard should restore both ViewModel and panel
        vm.Discard();
        var updatedModelBox = GetAllDescendants(panel).OfType<TextBox>()
            .FirstOrDefault(tb => tb.PlaceholderText == "Model");
        Assert.NotNull(updatedModelBox);
        Assert.Equal(SettingsModel.Defaults.Llm.Model, updatedModelBox.Text);
        Assert.Equal(SettingsModel.Defaults.Llm.Model, vm.Candidate.Llm.Model);
    }

    [Fact]
    public void ValidationErrors_AreDisplayedInErrorTextBlock()
    {
        using var settings = new TestSettingsService();
        using var vm = new SettingsViewModel(settings, new TestSecretStore());
        using var panel = new SettingsPanelView(vm);

        // Trigger a validation error
        vm.SetCodeFontFamily("");

        var textBlocks = GetAllDescendants(panel).OfType<TextBlock>().ToList();
        var errorBlock = textBlocks.FirstOrDefault(tb =>
            tb.Text is not null && tb.Text.Contains("Editor.CodeFontFamily"));
        Assert.NotNull(errorBlock);
    }

    [Fact]
    public async Task ConflictBanner_AppearsWhenExternalChangeDetected()
    {
        using var settings = new TestSettingsService();
        using var vm = new SettingsViewModel(settings, new TestSecretStore());
        using var panel = new SettingsPanelView(vm);

        // Modify candidate then simulate an external change
        vm.SetModel("user-model");
        settings.Publish(settings.Current with
        {
            Editor = settings.Current.Editor with { CodeFontSize = 42 }
        });

        // ApplyAsync detects the conflict because expectedCurrent != service.Current
        Assert.False(await vm.ApplyAsync());
        Assert.True(vm.HasConflict, "ViewModel should detect conflict after external change");

        var textBlocks = GetAllDescendants(panel).OfType<TextBlock>().ToList();
        var conflictLines = textBlocks
            .Where(tb => tb.Text is not null && tb.Text.Contains("outside this panel"))
            .ToList();
        Assert.NotEmpty(conflictLines);
        Assert.True(conflictLines[0].IsVisible, "Conflict banner should be visible");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static IEnumerable<Control> GetAllDescendants(Control parent)
    {
        foreach (var child in VisualChildren(parent))
        {
            yield return child;
            foreach (var descendant in GetAllDescendants(child))
                yield return descendant;
        }
    }

    private static IEnumerable<Control> VisualChildren(Control parent)
    {
        if (parent is Decorator decorator)
        {
            if (decorator.Child is Control child)
                yield return child;
        }
        else if (parent is Panel panel)
        {
            foreach (var child in panel.Children)
                if (child is Control c)
                    yield return c;
        }
        else if (parent is ContentControl contentControl)
        {
            if (contentControl.Content is Control c)
                yield return c;
        }
        else if (parent is ScrollViewer scrollViewer)
        {
            if (scrollViewer.Content is Control c)
                yield return c;
        }
        else if (parent is Popup popup)
        {
            if (popup.Child is Control c)
                yield return c;
        }
    }

    private static void EnsureApplication()
    {
        if (Application.Current is App app)
        {
            if (!app.Resources.ContainsKey("PrimaryAccentBrush")) app.Initialize();
            return;
        }
        new App().Initialize();
    }

    // ── Test doubles ──────────────────────────────────────────────────────

    private sealed class TestSecretStore : ISecretStore
    {
        private readonly Dictionary<string, string> _values = new();
        public string? Get(string key) => _values.TryGetValue(key, out var value) ? value : null;
        public void Set(string key, string value) => _values[key] = value;
        public void Delete(string key) => _values.Remove(key);
    }

    private sealed class TestSettingsService : ISettingsService, IDisposable
    {
        private readonly Subject<SettingsModel> _changes = new();
        public SettingsModel Current { get; private set; } = SettingsModel.Defaults;
        public IObservable<SettingsModel> WhenChanged => _changes;
        public SettingsLoadResult LoadResult => SettingsLoadResult.Missing;
        public IObservable<SettingsSaveError> WriteErrors => System.Reactive.Linq.Observable.Empty<SettingsSaveError>();

        public void Publish(SettingsModel snapshot)
        {
            Current = snapshot;
            _changes.OnNext(snapshot);
        }

        public Task<SettingsMutationResult> ApplyAsync(SettingsModel expectedCurrent, SettingsModel next, CancellationToken ct = default)
        {
            if (!ReferenceEquals(expectedCurrent, Current))
                return Task.FromResult<SettingsMutationResult>(new SettingsMutationResult.Conflict(Current));
            Current = next;
            return Task.FromResult<SettingsMutationResult>(new SettingsMutationResult.Applied(next, new SettingsSaveResult.Saved()));
        }
        public Task<SettingsMutationResult> UpdateAsync(Func<SettingsModel, SettingsModel> producer, CancellationToken ct = default) =>
            ApplyAsync(Current, producer(Current), ct);
        public Task<SettingsSaveResult> SaveAsync(CancellationToken ct = default) =>
            Task.FromResult<SettingsSaveResult>(new SettingsSaveResult.Saved());

        public void Dispose() => _changes.Dispose();
    }
}
