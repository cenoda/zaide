using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Zaide.Models;
using Zaide.Services;

namespace Zaide.Tests.Services;

/// <summary>
/// Phase 8.1.1 / M1 proof-of-concept tests covering JSON/schema validation,
/// atomic persistence, last-known-good recovery, migration infrastructure,
/// and rejection without overwriting invalid or unsupported source files.
/// </summary>
public sealed class Phase8ProofOfConceptTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;
    private readonly string _lastKnownGoodPath;
    private readonly string _tempPath;

    public Phase8ProofOfConceptTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ZaidePhase8POC_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "settings.json");
        _lastKnownGoodPath = Path.Combine(_tempDir, "settings.json.lastknowngood");
        _tempPath = Path.Combine(_tempDir, "settings.json.tmp");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private SettingsService CreateService()
    {
        return new SettingsService(_settingsPath, _lastKnownGoodPath, _tempPath,
            new SettingsMigrator(Array.Empty<ISettingsMigration>()));
    }

    private SettingsService CreateService(SettingsMigrator migrator)
    {
        return new SettingsService(_settingsPath, _lastKnownGoodPath, _tempPath, migrator);
    }

    private void WriteFile(string path, string content)
    {
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(path, content);
    }

    // ── Round trip ──────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_SerializesAndDeserializesCorrectly()
    {
        // Arrange
        WriteFile(_settingsPath, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            editor = new
            {
                codeFontFamily = "Custom Font",
                codeFontSize = 16,
                proseFontFamily = "Custom Serif",
                terminalFontFamily = "Custom Mono",
                terminalFontSize = 12,
                tabSize = 2,
                insertSpaces = false,
                showWhitespace = true,
                showTabs = true,
                showSpaces = true
            },
            llm = new
            {
                baseUrl = "https://custom.api.com",
                model = "custom-model",
                apiKeySource = "env"
            },
            keybindings = new { }
        }));

        // Act
        using var service = CreateService();

        // Assert
        Assert.Equal(SettingsLoadResult.Loaded, service.LoadResult);
        Assert.Equal("Custom Font", service.Current.Editor.CodeFontFamily);
        Assert.Equal(16, service.Current.Editor.CodeFontSize);
        Assert.Equal("Custom Serif", service.Current.Editor.ProseFontFamily);
        Assert.Equal("Custom Mono", service.Current.Editor.TerminalFontFamily);
        Assert.Equal(12, service.Current.Editor.TerminalFontSize);
        Assert.Equal(2, service.Current.Editor.TabSize);
        Assert.False(service.Current.Editor.InsertSpaces);
        Assert.True(service.Current.Editor.ShowWhitespace);
        Assert.True(service.Current.Editor.ShowTabs);
        Assert.True(service.Current.Editor.ShowSpaces);
        Assert.Equal("https://custom.api.com", service.Current.Llm.BaseUrl);
        Assert.Equal("custom-model", service.Current.Llm.Model);
        Assert.Equal("env", service.Current.Llm.ApiKeySource);
    }

    // ── Missing file ────────────────────────────────────────────────────

    [Fact]
    public void MissingFile_UsesDefaults()
    {
        using var service = CreateService();
        Assert.Equal(SettingsLoadResult.Missing, service.LoadResult);
        Assert.Equal(SettingsModel.Defaults, service.Current);
    }

    // ── Corrupt file ────────────────────────────────────────────────────

    [Fact]
    public void CorruptFile_FallsBackToLastKnownGood()
    {
        // Arrange: write a valid LKG and a corrupt main file.
        WriteFile(_lastKnownGoodPath, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            editor = new
            {
                codeFontFamily = "LKG Font",
                codeFontSize = 18,
                proseFontFamily = "Georgia, serif",
                terminalFontFamily = "Mono, monospace",
                terminalFontSize = 14,
                tabSize = 4,
                insertSpaces = true,
                showWhitespace = false,
                showTabs = false,
                showSpaces = false
            },
            llm = new
            {
                baseUrl = "https://lkg.example.com",
                model = "lkg-model",
                apiKeySource = "lkg"
            },
            keybindings = new { }
        }));
        WriteFile(_settingsPath, "this is not valid JSON {{{");

        // Act
        using var service = CreateService();

        // Assert: loaded LKG, but load result reflects the corrupt primary file.
        Assert.Equal(SettingsLoadResult.Corrupt, service.LoadResult);
        Assert.Equal("LKG Font", service.Current.Editor.CodeFontFamily);
        Assert.Equal(18, service.Current.Editor.CodeFontSize);
        Assert.Equal("https://lkg.example.com", service.Current.Llm.BaseUrl);
    }

    [Fact]
    public void CorruptFile_NoLastKnownGood_UsesDefaults()
    {
        // Arrange: corrupt main, no LKG.
        WriteFile(_settingsPath, "{{{ bad json");

        // Act
        using var service = CreateService();

        // Assert
        Assert.Equal(SettingsLoadResult.Corrupt, service.LoadResult);
        Assert.Equal(SettingsModel.Defaults, service.Current);
    }

    // ── Invalid schema (unsupported old version) ────────────────────────

    [Fact]
    public void UnsupportedOldVersion_FallsBackToDefaults()
    {
        // Arrange: schema v0 (unsupported).
        WriteFile(_settingsPath, JsonSerializer.Serialize(new
        {
            schemaVersion = 0,
            editor = new { },
            llm = new { },
            keybindings = new { }
        }));

        // Act
        using var service = CreateService();

        // Assert
        Assert.Equal(SettingsLoadResult.UnsupportedVersion, service.LoadResult);
        Assert.Equal(SettingsModel.Defaults, service.Current);
    }

    // ── Unknown future version ──────────────────────────────────────────

    [Fact]
    public void UnknownFutureVersion_FallsBackToDefaults()
    {
        // Arrange: schema v99 (unknown future).
        WriteFile(_settingsPath, JsonSerializer.Serialize(new
        {
            schemaVersion = 99,
            editor = new
            {
                codeFontFamily = "Future Font",
                codeFontSize = 20
            },
            llm = new
            {
                baseUrl = "https://future.api.com",
                model = "future-model",
                apiKeySource = "future"
            },
            keybindings = new { }
        }));

        // Act
        using var service = CreateService();

        // Assert
        Assert.Equal(SettingsLoadResult.UnsupportedVersion, service.LoadResult);
        Assert.Equal(SettingsModel.Defaults, service.Current);

        // Verify the source file was NOT overwritten.
        var onDisk = File.ReadAllText(_settingsPath);
        Assert.Contains("Future Font", onDisk);
    }

    // ── Rejected source file not overwritten during fallback ────────────

    [Fact]
    public void RejectedSourceFile_IsNotOverwrittenDuringFallback()
    {
        // Arrange
        WriteFile(_settingsPath, "corrupt data {{{");

        // Act
        using var service = CreateService();

        // Assert
        Assert.Equal(SettingsLoadResult.Corrupt, service.LoadResult);

        // The source file must still contain the original corrupt data.
        var onDisk = File.ReadAllText(_settingsPath);
        Assert.Equal("corrupt data {{{", onDisk);
    }

    [Fact]
    public void RejectedSourceFile_UnsupportedOld_IsNotOverwritten()
    {
        // Arrange: schema v0
        const string content = """{"schemaVersion":0,"editor":{},"llm":{},"keybindings":{}}""";
        WriteFile(_settingsPath, content);

        // Act
        using var service = CreateService();

        // Assert
        Assert.Equal(SettingsLoadResult.UnsupportedVersion, service.LoadResult);
        var onDisk = File.ReadAllText(_settingsPath);
        Assert.Equal(content, onDisk);
    }

    // ── Last-known-good updated on successful load ──────────────────────

    [Fact]
    public void LastKnownGood_UpdatedOnSuccessfulLoad()
    {
        // Arrange
        WriteFile(_settingsPath, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            editor = new
            {
                codeFontFamily = "Primary Font",
                codeFontSize = 14,
                proseFontFamily = "Georgia, serif",
                terminalFontFamily = "Mono, monospace",
                terminalFontSize = 14,
                tabSize = 4,
                insertSpaces = true,
                showWhitespace = false,
                showTabs = false,
                showSpaces = false
            },
            llm = new
            {
                baseUrl = "https://primary.example.com",
                model = "gpt-4o-mini",
                apiKeySource = "secret-store"
            },
            keybindings = new { }
        }));

        // Act
        using var service = CreateService();

        // Assert
        Assert.Equal(SettingsLoadResult.Loaded, service.LoadResult);

        // Last-known-good should now exist and be parseable.
        Assert.True(File.Exists(_lastKnownGoodPath));
        var lkgJson = File.ReadAllText(_lastKnownGoodPath);
        var lkgDoc = JsonDocument.Parse(lkgJson);
        Assert.Equal(1, lkgDoc.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("Primary Font", lkgDoc.RootElement.GetProperty("editor").GetProperty("codeFontFamily").GetString());
    }

    // ── Phase 8 D2: orphan temp does not override valid primary ─────────

    /// <summary>
    /// Phase 13 M2 / Phase 8 D2: when <c>settings.json</c> is valid and an
    /// orphan <c>settings.json.tmp</c> remains from an interrupted atomic write,
    /// the primary file stays authoritative. Load must not promote, overwrite,
    /// or delete the primary based on the orphan.
    /// </summary>
    [Fact]
    public void OrphanTemp_WithValidPrimary_PrimaryRemainsAuthoritative()
    {
        // Arrange: valid primary with known values.
        const string primaryJson = """
            {
              "schemaVersion": 1,
              "editor": {
                "codeFontFamily": "Primary Authority Font",
                "codeFontSize": 14,
                "proseFontFamily": "Georgia, serif",
                "terminalFontFamily": "Mono, monospace",
                "terminalFontSize": 14,
                "tabSize": 4,
                "insertSpaces": true,
                "showWhitespace": false,
                "showTabs": false,
                "showSpaces": false
              },
              "llm": {
                "baseUrl": "https://primary.example.com",
                "model": "primary-model",
                "apiKeySource": "secret-store"
              },
              "keybindings": {}
            }
            """;
        // Deliberately conflicting orphan temp from an interrupted write.
        const string orphanTempJson = """
            {
              "schemaVersion": 1,
              "editor": {
                "codeFontFamily": "Orphan Temp Font",
                "codeFontSize": 99,
                "proseFontFamily": "Orphan Serif",
                "terminalFontFamily": "Orphan Mono",
                "terminalFontSize": 99,
                "tabSize": 2,
                "insertSpaces": false,
                "showWhitespace": true,
                "showTabs": true,
                "showSpaces": true
              },
              "llm": {
                "baseUrl": "https://orphan-temp.example.com",
                "model": "orphan-temp-model",
                "apiKeySource": "orphan"
              },
              "keybindings": {}
            }
            """;
        WriteFile(_settingsPath, primaryJson);
        WriteFile(_tempPath, orphanTempJson);
        var primaryBytesBefore = File.ReadAllBytes(_settingsPath);
        var orphanBytesBefore = File.ReadAllBytes(_tempPath);

        // Act
        using var service = CreateService();

        // Assert: load returns primary values, not the orphan temp.
        Assert.Equal(SettingsLoadResult.Loaded, service.LoadResult);
        Assert.Equal("Primary Authority Font", service.Current.Editor.CodeFontFamily);
        Assert.Equal(14, service.Current.Editor.CodeFontSize);
        Assert.Equal("https://primary.example.com", service.Current.Llm.BaseUrl);
        Assert.Equal("primary-model", service.Current.Llm.Model);
        Assert.NotEqual("Orphan Temp Font", service.Current.Editor.CodeFontFamily);
        Assert.NotEqual(99, service.Current.Editor.CodeFontSize);
        Assert.NotEqual("orphan-temp-model", service.Current.Llm.Model);

        // Primary file bytes are unchanged (not overwritten or replaced by orphan).
        Assert.True(File.Exists(_settingsPath));
        Assert.Equal(primaryBytesBefore, File.ReadAllBytes(_settingsPath));

        // Orphan temp is not promoted over the primary and is not deleted by load.
        Assert.True(File.Exists(_tempPath));
        Assert.Equal(orphanBytesBefore, File.ReadAllBytes(_tempPath));
    }

    // ── Atomic write: temp-then-rename ──────────────────────────────────

    [Fact]
    public async Task AtomicWrite_WritesTempThenRenames()
    {
        // Arrange
        WriteFile(_settingsPath, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            editor = new
            {
                codeFontFamily = "Original",
                codeFontSize = 14,
                proseFontFamily = "Georgia, serif",
                terminalFontFamily = "Mono, monospace",
                terminalFontSize = 14,
                tabSize = 4,
                insertSpaces = true,
                showWhitespace = false,
                showTabs = false,
                showSpaces = false
            },
            llm = new
            {
                baseUrl = "https://original.example.com",
                model = "gpt-4o-mini",
                apiKeySource = "secret-store"
            },
            keybindings = new { }
        }));
        using var service = CreateService();

        // Act
        var next = service.Current with
        {
            Editor = service.Current.Editor with { CodeFontSize = 20 }
        };
        var result = await service.UpdateAsync(_ => next);

        // Assert
        var applied = Assert.IsType<SettingsMutationResult.Applied>(result);
        Assert.IsType<SettingsSaveResult.Saved>(applied.SaveResult);

        // The temp file should NOT exist after a successful write.
        Assert.False(File.Exists(_tempPath));

        // The main file should contain the updated value.
        var onDisk = File.ReadAllText(_settingsPath);
        var doc = JsonDocument.Parse(onDisk);
        Assert.Equal(20, doc.RootElement.GetProperty("editor").GetProperty("codeFontSize").GetInt32());

        // LKG should also be updated.
        Assert.True(File.Exists(_lastKnownGoodPath));
    }

    // ── Synthetic migration ─────────────────────────────────────────────

    private sealed class SyntheticV1ToV2Migration : ISettingsMigration
    {
        public int FromVersion => 1;
        public int ToVersion => 2;

        public SettingsModel Migrate(SettingsModel model)
        {
            // Add a marker in the LLM model name that migration ran.
            return model with
            {
                SchemaVersion = 2,
                Llm = model.Llm with { Model = "migrated-" + model.Llm.Model }
            };
        }
    }

    [Fact]
    public void SyntheticMigration_RunsAndTransformsModel()
    {
        // Arrange: write a v1 file.
        WriteFile(_settingsPath, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            editor = new
            {
                codeFontFamily = "Cascadia Code, Consolas, monospace",
                codeFontSize = 14,
                proseFontFamily = "Georgia, serif",
                terminalFontFamily = "Cascadia Code, JetBrains Mono, DejaVu Sans Mono, monospace",
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

        var migrator = new SettingsMigrator(new List<ISettingsMigration>
        {
            new SyntheticV1ToV2Migration()
        });

        // Act
        using var service = CreateService(migrator);

        // Assert: migration ran.
        Assert.Equal(2, service.Current.SchemaVersion);
        Assert.StartsWith("migrated-", service.Current.Llm.Model);
        Assert.Contains("gpt-4o-mini", service.Current.Llm.Model);
    }

    [Fact]
    public void SyntheticMigration_LastKnownGood_IsMigratedCopy()
    {
        // Arrange: write a v1 file.
        WriteFile(_settingsPath, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            editor = new
            {
                codeFontFamily = "Cascadia Code, Consolas, monospace",
                codeFontSize = 14,
                proseFontFamily = "Georgia, serif",
                terminalFontFamily = "Cascadia Code, JetBrains Mono, DejaVu Sans Mono, monospace",
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

        var migrator = new SettingsMigrator(new List<ISettingsMigration>
        {
            new SyntheticV1ToV2Migration()
        });

        // Act
        using var service = CreateService(migrator);

        // Assert: LKG file contains the migrated (v2) model.
        Assert.True(File.Exists(_lastKnownGoodPath));
        var lkgJson = File.ReadAllText(_lastKnownGoodPath);
        var lkgDoc = JsonDocument.Parse(lkgJson);
        Assert.Equal(2, lkgDoc.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.StartsWith("migrated-",
            lkgDoc.RootElement.GetProperty("llm").GetProperty("model").GetString());
    }
}
