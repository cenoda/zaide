using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;
using Zaide.App.Composition;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Settings.Infrastructure;
using Zaide.Features.Settings.Presentation;

namespace Zaide.Tests.Features.Settings.Infrastructure;

/// <summary>
/// Phase 8.2 / M7b tests for the immutable keybindings settings schema.
///
/// Covers the flat <c>commandId → neutralGesture</c> dictionary contract,
/// schema v1 JSON round-trip, empty-string unbind semantics, rejection of
/// missing/null/malformed keybindings sections, and defensive copying at both
/// the deserialization and candidate-publication snapshot boundaries.
/// </summary>
public sealed class SettingsKeybindingsSchemaTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;
    private readonly string _lastKnownGoodPath;
    private readonly string _tempPath;

    public SettingsKeybindingsSchemaTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ZaideKeybindings_" + Guid.NewGuid().ToString("N"));
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

    private SettingsService CreateService()
    {
        return new SettingsService(_settingsPath, _lastKnownGoodPath, _tempPath,
            new SettingsMigrator(Array.Empty<ISettingsMigration>()));
    }

    private static SettingsModel WithKeybindings(
        IReadOnlyDictionary<string, string> keybindings) =>
        SettingsModel.Defaults with { Keybindings = keybindings };

    // ── Defaults ────────────────────────────────────────────────────────

    [Fact]
    public void Defaults_HaveEmptyReadOnlyKeybindings()
    {
        var def = SettingsModel.Defaults;

        Assert.NotNull(def.Keybindings);
        Assert.Empty(def.Keybindings);
        Assert.IsType<ReadOnlyDictionary<string, string>>(def.Keybindings);
    }

    // ── Flat JSON round-trip ────────────────────────────────────────────

    [Fact]
    public void FlatJson_RoundTrips()
    {
        var model = WithKeybindings(new Dictionary<string, string>
        {
            ["file.save"] = "Ctrl+Shift+S",
            ["explorer.toggleHiddenFiles"] = ""
        });

        var json = SettingsSerializer.Serialize(model);
        Assert.Contains("\"keybindings\"", json);

        var parsed = SettingsSerializer.Deserialize(json, out var schemaRejected);

        Assert.False(schemaRejected);
        Assert.NotNull(parsed);
        Assert.Equal("Ctrl+Shift+S", parsed!.Keybindings["file.save"]);
        Assert.Equal("", parsed.Keybindings["explorer.toggleHiddenFiles"]);
    }

    // ── Multiple overrides round-trip ───────────────────────────────────

    [Fact]
    public void MultipleCommandOverrides_RoundTrip()
    {
        var model = WithKeybindings(new Dictionary<string, string>
        {
            ["file.save"] = "Ctrl+Shift+S",
            ["workspace.openFolder"] = "Ctrl+O",
            ["view.toggleBottomPanel"] = "Ctrl+J",
            ["sourcecontrol.commit"] = "Ctrl+Alt+C"
        });

        var json = SettingsSerializer.Serialize(model);
        var parsed = SettingsSerializer.Deserialize(json, out _);

        Assert.NotNull(parsed);
        Assert.Equal(4, parsed!.Keybindings.Count);
        Assert.Equal("Ctrl+Shift+S", parsed.Keybindings["file.save"]);
        Assert.Equal("Ctrl+O", parsed.Keybindings["workspace.openFolder"]);
        Assert.Equal("Ctrl+J", parsed.Keybindings["view.toggleBottomPanel"]);
        Assert.Equal("Ctrl+Alt+C", parsed.Keybindings["sourcecontrol.commit"]);
    }

    // ── Empty-string unbind round-trip ──────────────────────────────────

    [Fact]
    public void EmptyStringUnbind_RoundTrips_AsExplicitUnbind()
    {
        // Arrange: a command mapped to an empty string must survive the
        // round-trip as an explicit empty value, not as a missing override.
        var model = WithKeybindings(new Dictionary<string, string>
        {
            ["explorer.toggleHiddenFiles"] = ""
        });

        var json = SettingsSerializer.Serialize(model);

        // Assert the serialized JSON carries the empty-string value verbatim.
        using var doc = JsonDocument.Parse(json);
        var value = doc.RootElement
            .GetProperty("keybindings")
            .GetProperty("explorer.toggleHiddenFiles");
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        Assert.Equal("", value.GetString());

        var parsed = SettingsSerializer.Deserialize(json, out _);

        Assert.NotNull(parsed);
        Assert.True(parsed!.Keybindings.ContainsKey("explorer.toggleHiddenFiles"));
        Assert.Equal("", parsed.Keybindings["explorer.toggleHiddenFiles"]);
    }

    // ── Rejection of missing / null / malformed sections ────────────────

    [Fact]
    public void MissingKeybindingsSection_IsRejected()
    {
        var json = SettingsSerializer.Serialize(SettingsModel.Defaults);
        var node = JsonNode.Parse(json)!;
        node.AsObject().Remove("keybindings");

        var parsed = SettingsSerializer.Deserialize(node.ToJsonString(), out var schemaRejected);

        Assert.Null(parsed);
        Assert.False(schemaRejected);
    }

    [Fact]
    public void NullKeybindingsSection_IsRejected()
    {
        var json = SettingsSerializer.Serialize(SettingsModel.Defaults);
        var node = JsonNode.Parse(json)!;
        node.AsObject()["keybindings"] = null;

        var parsed = SettingsSerializer.Deserialize(node.ToJsonString(), out var schemaRejected);

        Assert.Null(parsed);
        Assert.False(schemaRejected);
    }

    [Fact]
    public void MalformedKeybindingsValues_AreRejected()
    {
        // A non-string override value (a number here) is malformed and must
        // be rejected by the serializer.
        var json = SettingsSerializer.Serialize(SettingsModel.Defaults);
        var node = JsonNode.Parse(json)!;
        node.AsObject()["keybindings"] = JsonNode.Parse(@"{ ""file.save"": 123 }");

        var parsed = SettingsSerializer.Deserialize(node.ToJsonString(), out var schemaRejected);

        Assert.Null(parsed);
        Assert.False(schemaRejected);
    }

    // ── Defensive copy on deserialization ──────────────────────────────

    [Fact]
    public void Deserialize_DefensivelyCopies_OriginalMutationDoesNotAffectModel()
    {
        var original = new Dictionary<string, string> { ["file.save"] = "Ctrl+S" };
        var model = WithKeybindings(original);
        var json = SettingsSerializer.Serialize(model);

        var parsed = SettingsSerializer.Deserialize(json, out _)!;

        // The published dictionary is a read-only wrapper.
        Assert.IsType<ReadOnlyDictionary<string, string>>(parsed.Keybindings);

        // Mutating the original input dictionary must not affect the model.
        original["file.save"] = "Ctrl+Z";
        original["file.open"] = "Ctrl+O";

        Assert.Equal("Ctrl+S", parsed.Keybindings["file.save"]);
        Assert.False(parsed.Keybindings.ContainsKey("file.open"));
    }

    [Fact]
    public void Deserialize_ReadOnlyDictionaryWithMutableBacking_DoesNotLeak()
    {
        // Arrange: simulate a candidate whose keybindings is a ReadOnlyDictionary
        // wrapping a mutable backing store the caller still owns.
        var backing = new Dictionary<string, string> { ["file.save"] = "Ctrl+S" };
        var readOnlyView = new ReadOnlyDictionary<string, string>(backing);
        var model = WithKeybindings(readOnlyView);

        var json = SettingsSerializer.Serialize(model);
        var parsed = SettingsSerializer.Deserialize(json, out _)!;

        // Act: mutate the external backing store after serialization/deserialization.
        backing["file.save"] = "Ctrl+Z";
        backing["file.open"] = "Ctrl+O";

        // Assert: the published (parsed) snapshot is unaffected.
        Assert.IsType<ReadOnlyDictionary<string, string>>(parsed.Keybindings);
        Assert.Equal("Ctrl+S", parsed.Keybindings["file.save"]);
        Assert.False(parsed.Keybindings.ContainsKey("file.open"));
    }

    [Fact]
    public async Task CandidatePublication_ReadOnlyDictionaryWithMutableBacking_DoesNotLeak()
    {
        await File.WriteAllTextAsync(_settingsPath,
            SettingsSerializer.Serialize(SettingsModel.Defaults));
        using var service = CreateService();

        var backing = new Dictionary<string, string> { ["file.save"] = "Ctrl+S" };
        var readOnlyView = new ReadOnlyDictionary<string, string>(backing);

        var result = await service.UpdateAsync(s => s with { Keybindings = readOnlyView });

        Assert.IsType<SettingsMutationResult.Applied>(result);
        Assert.IsType<ReadOnlyDictionary<string, string>>(service.Current.Keybindings);

        // Act: mutate the external backing store after publication.
        backing["file.save"] = "Ctrl+Z";
        backing["file.open"] = "Ctrl+O";

        // Assert: the published snapshot is unaffected.
        Assert.Equal("Ctrl+S", service.Current.Keybindings["file.save"]);
        Assert.False(service.Current.Keybindings.ContainsKey("file.open"));
    }

    // ── Defensive copy on candidate publication ─────────────────────────

    [Fact]
    public async Task CandidatePublication_DefensivelyCopies_OriginalMutationDoesNotAffectPublishedModel()
    {
        await File.WriteAllTextAsync(_settingsPath,
            SettingsSerializer.Serialize(SettingsModel.Defaults));
        using var service = CreateService();

        var original = new Dictionary<string, string> { ["file.save"] = "Ctrl+S" };

        var result = await service.UpdateAsync(s => s with { Keybindings = original });

        Assert.IsType<SettingsMutationResult.Applied>(result);
        Assert.IsType<ReadOnlyDictionary<string, string>>(service.Current.Keybindings);

        // Mutating the caller's dictionary after publication must not leak in.
        original["file.save"] = "Ctrl+Z";
        original["file.open"] = "Ctrl+O";

        Assert.Equal("Ctrl+S", service.Current.Keybindings["file.save"]);
        Assert.False(service.Current.Keybindings.ContainsKey("file.open"));
    }

    [Fact]
    public async Task PublishedKeybindings_CannotBeMutatedViaCast()
    {
        await File.WriteAllTextAsync(_settingsPath,
            SettingsSerializer.Serialize(SettingsModel.Defaults));
        using var service = CreateService();

        var mutable = new Dictionary<string, string> { ["file.save"] = "Ctrl+S" };
        await service.UpdateAsync(s => s with { Keybindings = mutable });

        var published = service.Current.Keybindings;

        // A retained/cast reference must not allow mutation.
        var asMutable = (IDictionary<string, string>)published;
        Assert.Throws<NotSupportedException>(() => asMutable.Add("file.open", "Ctrl+O"));
        Assert.Throws<NotSupportedException>(() => asMutable.Clear());
        Assert.Throws<NotSupportedException>(() => asMutable["file.save"] = "Ctrl+Z");

        Assert.Equal("Ctrl+S", service.Current.Keybindings["file.save"]);
        Assert.False(service.Current.Keybindings.ContainsKey("file.open"));
    }

    [Fact]
    public async Task CandidatePublication_PersistedKeybindingsRoundTrip()
    {
        await File.WriteAllTextAsync(_settingsPath,
            SettingsSerializer.Serialize(SettingsModel.Defaults));
        using var service = CreateService();

        await service.UpdateAsync(s => s with
        {
            Keybindings = new Dictionary<string, string>
            {
                ["file.save"] = "Ctrl+Shift+S",
                ["explorer.toggleHiddenFiles"] = ""
            }
        });

        // The on-disk file must contain the persisted keybindings.
        var onDisk = File.ReadAllText(_settingsPath);
        var reparsed = SettingsSerializer.Deserialize(onDisk, out _);

        Assert.NotNull(reparsed);
        Assert.Equal("Ctrl+Shift+S", reparsed!.Keybindings["file.save"]);
        Assert.Equal("", reparsed.Keybindings["explorer.toggleHiddenFiles"]);
    }
}
