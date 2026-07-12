using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zaide.Models;

namespace Zaide.Services;

/// <summary>
/// JSON serialization for the settings model (schema v1).
/// </summary>
internal static class SettingsSerializer
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        AllowTrailingCommas = false,
    };

    /// <summary>Serialize to JSON string.</summary>
    public static string Serialize(SettingsModel settings)
    {
        return JsonSerializer.Serialize(settings, WriteOptions);
    }

    /// <summary>
    /// Deserialize from JSON string.
    /// </summary>
    /// <param name="json">Raw JSON text.</param>
    /// <param name="schemaRejected">
    /// Set to <c>true</c> when the JSON was structurally valid but its
    /// <c>schemaVersion</c> is not supported (too old or too new).
    /// When <c>false</c> and the result is <c>null</c>, the JSON was
    /// unparseable or structurally invalid.
    /// </param>
    /// <returns>The deserialized model, or <c>null</c> on failure.</returns>
    public static SettingsModel? Deserialize(string json, out bool schemaRejected)
    {
        schemaRejected = false;

        try
        {
            // First pass: extract schemaVersion
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false
            });

            var root = doc.RootElement;

            // schemaVersion must be present and numeric
            if (!root.TryGetProperty("schemaVersion", out var svProp) ||
                svProp.ValueKind != JsonValueKind.Number ||
                !svProp.TryGetInt32(out var schemaVersion))
            {
                return null;
            }

            // Reject unknown future versions and unsupported old versions
            if (schemaVersion is < 1 or > 1)
            {
                schemaRejected = true;
                return null;
            }

            // Second pass: full deserialization
            var result = JsonSerializer.Deserialize<SettingsModel>(json, ReadOptions);
            if (result is null)
                return null;

            // Schema version 1 must have all required sections.
            if (result.Editor is null || result.Llm is null)
                return null;

            // A missing or null keybindings section is rejected. An empty or
            // populated section is normalized into a defensive read-only copy.
            if (result.Keybindings is null)
                return null;

            return result with
            {
                Keybindings = SettingsModel.NormalizeKeybindings(result.Keybindings)
            };
        }
        catch (JsonException)
        {
            return null;
        }
        catch (ArgumentNullException)
        {
            return null;
        }
    }
}
