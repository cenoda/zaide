using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.ViewModels;
using Zaide.Tests.Services;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Infrastructure;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Editor.Presentation;

namespace Zaide.Tests.Features.Editor.Infrastructure;

/// <summary>
/// Phase 10 M6 Format-on-Save execution contract and settings schema tests.
/// </summary>
public sealed class FormatOnSaveTests
{
    private static readonly string TempRoot = Path.Combine(
        Path.GetTempPath(),
        "zaide-phase10-m6-fos-" + Guid.NewGuid().ToString("N"));

    static FormatOnSaveTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
        Directory.CreateDirectory(TempRoot);
    }

    private sealed class RecordingFileService : IFileService
    {
        public List<(string Path, string Content)> Writes { get; } = new();
        public int WriteCount => Writes.Count;

        public Task WriteAllTextAsync(string path, string contents)
        {
            Writes.Add((path, contents));
            return Task.CompletedTask;
        }

        public Task<string> ReadAllTextAsync(string path) =>
            Task.FromResult(File.Exists(path) ? File.ReadAllText(path) : string.Empty);
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        private readonly Subject<SettingsModel> _whenChanged = new();
        private SettingsModel _current;

        public FakeSettingsService(SettingsModel current) => _current = current;

        public SettingsModel Current => _current;
        public IObservable<SettingsModel> WhenChanged => _whenChanged;
        public SettingsLoadResult LoadResult => SettingsLoadResult.Loaded;
        public IObservable<SettingsSaveError> WriteErrors => Observable.Empty<SettingsSaveError>();

        public void Set(SettingsModel model)
        {
            _current = model;
            _whenChanged.OnNext(model);
        }

        public Task<SettingsMutationResult> UpdateAsync(
            Func<SettingsModel, SettingsModel> producer,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<SettingsMutationResult> ApplyAsync(
            SettingsModel expectedCurrent,
            SettingsModel next,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<SettingsSaveResult> SaveAsync(CancellationToken ct = default) =>
            Task.FromResult<SettingsSaveResult>(new SettingsSaveResult.Saved());

        public void Dispose()
        {
            _whenChanged.OnCompleted();
            _whenChanged.Dispose();
        }
    }

    private sealed class FakeFormattingService : ILanguageFormattingService
    {
        private readonly Subject<LanguageFormattingSnapshot> _subject = new();
        public Func<string, CancellationToken, Task<LanguageFormattingOutcome>>? Handler { get; set; }
        public int CallCount { get; private set; }
        public List<string> Paths { get; } = new();

        public LanguageFormattingSnapshot Current { get; private set; } = LanguageFormattingSnapshot.Idle;
        public IObservable<LanguageFormattingSnapshot> WhenChanged => _subject;

        public async Task<LanguageFormattingOutcome> FormatDocumentAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Paths.Add(filePath);
            if (Handler is not null)
                return await Handler(filePath, cancellationToken).ConfigureAwait(false);

            return LanguageFormattingOutcome.Terminal(
                LanguageFormattingOutcomeKind.NoEdits,
                LanguageFormattingPolicy.NoEditsMessage);
        }

        public void Dispose()
        {
            _subject.OnCompleted();
            _subject.Dispose();
        }
    }

    private static SettingsModel WithFormatOnSave(bool enabled) =>
        SettingsModel.Defaults with
        {
            Editor = EditorSettings.Default with { FormatOnSave = enabled },
        };

    [Fact]
    public async Task FormatOnSave_Disabled_DoesNotCallFormatting()
    {
        var path = Path.Combine(TempRoot, "off.cs");
        File.WriteAllText(path, "original");
        var files = new RecordingFileService();
        var settings = new FakeSettingsService(WithFormatOnSave(false));
        var formatting = new FakeFormattingService();
        var vm = new EditorViewModel(new Document(path, "original"), files, settings, formatting);
        vm.TextContent = "edited";
        Assert.True(vm.IsDirty);

        var ok = await vm.SaveCommand.Execute().FirstAsync();

        Assert.True(ok);
        Assert.Equal(0, formatting.CallCount);
        Assert.Single(files.Writes);
        Assert.Equal("edited", files.Writes[0].Content);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task FormatOnSave_Enabled_FormatsBeforeWrite_WritesFormattedOnce()
    {
        var path = Path.Combine(TempRoot, "on.cs");
        File.WriteAllText(path, "messy");
        var files = new RecordingFileService();
        var settings = new FakeSettingsService(WithFormatOnSave(true));
        var formatting = new FakeFormattingService
        {
            Handler = (_, _) => Task.FromResult(new LanguageFormattingOutcome(
                LanguageFormattingOutcomeKind.Applied,
                "formatted",
                Array.Empty<LanguageTextEdit>(),
                LanguageFormattingPolicy.AppliedMessage)),
        };
        var vm = new EditorViewModel(new Document(path, "messy"), files, settings, formatting);
        vm.TextContent = "messy-dirty";
        Assert.True(vm.IsDirty);

        var ok = await vm.SaveCommand.Execute().FirstAsync();

        Assert.True(ok);
        Assert.Equal(1, formatting.CallCount);
        Assert.Single(files.Writes);
        Assert.Equal("formatted", files.Writes[0].Content);
        Assert.Equal("formatted", vm.TextContent);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task FormatOnSave_Failure_StillSavesCurrentContent()
    {
        var path = Path.Combine(TempRoot, "fail.cs");
        File.WriteAllText(path, "x");
        var files = new RecordingFileService();
        var settings = new FakeSettingsService(WithFormatOnSave(true));
        var formatting = new FakeFormattingService
        {
            Handler = (_, _) => Task.FromResult(LanguageFormattingOutcome.Terminal(
                LanguageFormattingOutcomeKind.Failed,
                LanguageFormattingPolicy.FailedMessage)),
        };
        var vm = new EditorViewModel(new Document(path, "current"), files, settings, formatting);

        var ok = await vm.SaveCommand.Execute().FirstAsync();

        Assert.True(ok);
        Assert.Equal(1, formatting.CallCount);
        Assert.Single(files.Writes);
        Assert.Equal("current", files.Writes[0].Content);
        Assert.Equal("current", vm.TextContent);
    }

    [Fact]
    public async Task FormatOnSave_Cancelled_StillSavesCurrentContent()
    {
        var path = Path.Combine(TempRoot, "cancel.cs");
        File.WriteAllText(path, "x");
        var files = new RecordingFileService();
        var settings = new FakeSettingsService(WithFormatOnSave(true));
        var formatting = new FakeFormattingService
        {
            Handler = (_, _) => Task.FromResult(LanguageFormattingOutcome.Terminal(
                LanguageFormattingOutcomeKind.Cancelled,
                LanguageFormattingPolicy.CancelledMessage)),
        };
        var vm = new EditorViewModel(new Document(path, "keep"), files, settings, formatting);

        var ok = await vm.SaveCommand.Execute().FirstAsync();

        Assert.True(ok);
        Assert.Single(files.Writes);
        Assert.Equal("keep", files.Writes[0].Content);
    }

    [Fact]
    public async Task FormatOnSave_Unsupported_StillSavesCurrentContent()
    {
        var path = Path.Combine(TempRoot, "unsup.cs");
        File.WriteAllText(path, "x");
        var files = new RecordingFileService();
        var settings = new FakeSettingsService(WithFormatOnSave(true));
        var formatting = new FakeFormattingService
        {
            Handler = (_, _) => Task.FromResult(LanguageFormattingOutcome.Terminal(
                LanguageFormattingOutcomeKind.Unsupported,
                LanguageFormattingPolicy.UnsupportedMessage)),
        };
        var vm = new EditorViewModel(new Document(path, "keep"), files, settings, formatting);

        var ok = await vm.SaveCommand.Execute().FirstAsync();

        Assert.True(ok);
        Assert.Single(files.Writes);
        Assert.Equal("keep", files.Writes[0].Content);
    }

    [Fact]
    public async Task FormatOnSave_NoDoubleWrite_WhenFormattingSucceeds()
    {
        var path = Path.Combine(TempRoot, "once.cs");
        File.WriteAllText(path, "a");
        var files = new RecordingFileService();
        var settings = new FakeSettingsService(WithFormatOnSave(true));
        var formatting = new FakeFormattingService
        {
            Handler = (_, _) => Task.FromResult(new LanguageFormattingOutcome(
                LanguageFormattingOutcomeKind.Applied,
                "b",
                Array.Empty<LanguageTextEdit>(),
                null)),
        };
        var vm = new EditorViewModel(new Document(path, "a"), files, settings, formatting);

        await vm.SaveCommand.Execute().FirstAsync();

        Assert.Equal(1, files.WriteCount);
        Assert.Equal(1, formatting.CallCount);
    }

    [Fact]
    public void SettingsDefaults_SchemaV3_FormatOnSaveFalse()
    {
        Assert.Equal(3, SettingsModel.Defaults.SchemaVersion);
        Assert.False(SettingsModel.Defaults.Editor.FormatOnSave);
        Assert.False(EditorSettings.Default.FormatOnSave);
        Assert.Empty(SettingsModel.Defaults.Debug.BreakpointsByWorkspaceRoot);
    }

    [Fact]
    public void Serializer_V3RoundTrip_IncludesFormatOnSave()
    {
        var model = SettingsModel.Defaults with
        {
            Editor = EditorSettings.Default with { FormatOnSave = true },
        };
        var json = SettingsSerializer.Serialize(model);
        Assert.Contains("\"formatOnSave\": true", json, StringComparison.Ordinal);

        var parsed = SettingsSerializer.Deserialize(json, out var rejected);
        Assert.False(rejected);
        Assert.NotNull(parsed);
        Assert.Equal(3, parsed!.SchemaVersion);
        Assert.True(parsed.Editor.FormatOnSave);
    }

    [Fact]
    public void Serializer_FutureSchema_Rejected()
    {
        var json = """
            {
              "schemaVersion": 4,
              "editor": {
                "codeFontFamily": "x",
                "codeFontSize": 14,
                "proseFontFamily": "y",
                "terminalFontFamily": "z",
                "terminalFontSize": 14,
                "tabSize": 4,
                "insertSpaces": true,
                "showWhitespace": false,
                "showTabs": false,
                "showSpaces": false,
                "formatOnSave": false
              },
              "llm": {
                "baseUrl": "https://example.com",
                "model": "m",
                "apiKeySource": "secret-store"
              },
              "keybindings": {},
              "debug": {
                "breakpointsByWorkspaceRoot": {}
              }
            }
            """;

        var parsed = SettingsSerializer.Deserialize(json, out var rejected);
        Assert.Null(parsed);
        Assert.True(rejected);
    }

    [Fact]
    public void Serializer_V3RoundTrip_IncludesDebugBreakpoints()
    {
        var workspaceRoot = Path.GetFullPath(Path.Combine(TempRoot, "ws"));
        var sourcePath = Path.GetFullPath(Path.Combine(workspaceRoot, "Program.cs"));
        var model = SettingsModel.Defaults with
        {
            Debug = new DebugSettings(new Dictionary<string, IReadOnlyList<PersistedBreakpoint>>
            {
                [workspaceRoot] = new[]
                {
                    new PersistedBreakpoint(sourcePath, 8, true),
                    new PersistedBreakpoint(sourcePath, 15, false),
                },
            }),
        };

        var json = SettingsSerializer.Serialize(model);
        Assert.Contains("\"breakpointsByWorkspaceRoot\"", json, StringComparison.Ordinal);

        var parsed = SettingsSerializer.Deserialize(json, out var rejected);
        Assert.False(rejected);
        Assert.NotNull(parsed);
        Assert.Equal(3, parsed!.SchemaVersion);
        Assert.True(parsed.Debug.BreakpointsByWorkspaceRoot.ContainsKey(workspaceRoot));
        Assert.Equal(2, parsed.Debug.BreakpointsByWorkspaceRoot[workspaceRoot].Count);
        Assert.Equal(8, parsed.Debug.BreakpointsByWorkspaceRoot[workspaceRoot][0].Line);
        Assert.False(parsed.Debug.BreakpointsByWorkspaceRoot[workspaceRoot][1].Enabled);
    }

    [Fact]
    public void Serializer_V1WithoutFormatOnSave_Deserializes()
    {
        var json = """
            {
              "schemaVersion": 1,
              "editor": {
                "codeFontFamily": "Cascadia Code, Consolas, monospace",
                "codeFontSize": 14,
                "proseFontFamily": "Georgia, serif",
                "terminalFontFamily": "Cascadia Code, JetBrains Mono, DejaVu Sans Mono, monospace",
                "terminalFontSize": 14,
                "tabSize": 4,
                "insertSpaces": true,
                "showWhitespace": false,
                "showTabs": false,
                "showSpaces": false
              },
              "llm": {
                "baseUrl": "https://api.openai.com/v1",
                "model": "gpt-4o-mini",
                "apiKeySource": "secret-store"
              },
              "keybindings": {}
            }
            """;

        var parsed = SettingsSerializer.Deserialize(json, out var rejected);
        Assert.False(rejected);
        Assert.NotNull(parsed);
        Assert.Equal(1, parsed!.SchemaVersion);
        Assert.False(parsed.Editor.FormatOnSave);
    }

    [Fact]
    public void Migration_V1ToV2_AddsFormatOnSaveFalse_PreservesOtherFields()
    {
        var v1 = new SettingsModel(
            SchemaVersion: 1,
            Editor: new EditorSettings(
                "MyFont", 16, "Prose", "Term", 12, 2, false, true, true, false),
            Llm: new LlmSettings("https://custom", "custom-model", "secret-store"),
            Keybindings: new Dictionary<string, string> { ["palette.open"] = "Ctrl+P" },
            Debug: DebugSettings.Default);

        var migration = new SettingsMigrationV1ToV2();
        Assert.Equal(1, migration.FromVersion);
        Assert.Equal(2, migration.ToVersion);

        var v2 = migration.Migrate(v1);
        Assert.Equal(2, v2.SchemaVersion);
        Assert.False(v2.Editor.FormatOnSave);
        Assert.Equal("MyFont", v2.Editor.CodeFontFamily);
        Assert.Equal(16, v2.Editor.CodeFontSize);
        Assert.Equal("https://custom", v2.Llm.BaseUrl);
        Assert.Equal("custom-model", v2.Llm.Model);
        Assert.Equal("Ctrl+P", v2.Keybindings["palette.open"]);
    }

    [Fact]
    public void Migration_V2ToV3_AddsEmptyBreakpointMap_PreservesOtherFields()
    {
        var v2 = SettingsModel.Defaults with { SchemaVersion = 2 };
        var migration = new SettingsMigrationV2ToV3();
        Assert.Equal(2, migration.FromVersion);
        Assert.Equal(3, migration.ToVersion);

        var v3 = migration.Migrate(v2);
        Assert.Equal(3, v3.SchemaVersion);
        Assert.Empty(v3.Debug.BreakpointsByWorkspaceRoot);
        Assert.False(v3.Editor.FormatOnSave);
        Assert.Equal(SettingsModel.Defaults.Llm.Model, v3.Llm.Model);
    }

    [Fact]
    public void Migration_ViaSettingsService_LoadsV1FileAsV3()
    {
        var dir = Path.Combine(TempRoot, "mig-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var settingsPath = Path.Combine(dir, "settings.json");
        var lkgPath = Path.Combine(dir, "settings.last-known-good.json");
        var tempPath = Path.Combine(dir, "settings.tmp");

        File.WriteAllText(settingsPath, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            editor = new
            {
                codeFontFamily = "Cascadia Code, Consolas, monospace",
                codeFontSize = 20,
                proseFontFamily = "Georgia, serif",
                terminalFontFamily = "monospace",
                terminalFontSize = 14,
                tabSize = 4,
                insertSpaces = true,
                showWhitespace = false,
                showTabs = false,
                showSpaces = false
            },
            llm = new
            {
                baseUrl = "https://api.openai.com/v1",
                model = "gpt-4o-mini",
                apiKeySource = "secret-store"
            },
            keybindings = new { }
        }));

        var migrator = new SettingsMigrator(new ISettingsMigration[]
        {
            new SettingsMigrationV1ToV2(),
            new SettingsMigrationV2ToV3(),
        });
        using var service = new SettingsService(settingsPath, lkgPath, tempPath, migrator);

        Assert.Equal(3, service.Current.SchemaVersion);
        Assert.False(service.Current.Editor.FormatOnSave);
        Assert.Equal(20, service.Current.Editor.CodeFontSize);
        Assert.Empty(service.Current.Debug.BreakpointsByWorkspaceRoot);
    }

    [Fact]
    public void EditApplier_MultiEdit_OrderingAndDirtyIndependence()
    {
        // Pure applier proof used by FoS and explicit format paths.
        var source = "int a;int b;";
        var edits = new[]
        {
            new LanguageTextEdit(new LspRange(0, 0, 0, 6), "int a; "),
            new LanguageTextEdit(new LspRange(0, 6, 0, 12), "int b;"),
        };
        Assert.True(LanguageFormattingEditApplier.TryApply(source, edits, out var formatted));
        Assert.Equal("int a; int b;", formatted);
    }
}
