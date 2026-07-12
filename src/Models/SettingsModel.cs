using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Zaide.Models;

/// <summary>
/// Deeply immutable root settings model for the Zaide application.
/// All nested types are also immutable records. Consumers create new instances
/// via <c>with</c> expressions and cannot mutate a published snapshot.
/// </summary>
/// <param name="SchemaVersion">Version of the settings schema (currently <c>1</c>).</param>
/// <param name="Editor">Editor/terminal display preferences.</param>
/// <param name="Llm">LLM endpoint configuration.</param>
/// <param name="Keybindings">
/// User keybinding overrides as a flat <c>commandId → neutralGesture</c> map.
/// An empty-string value explicitly unbinds the command; it is not treated as a
/// missing override. The dictionary is always exposed as a read-only wrapper.
/// </param>
public sealed record SettingsModel(
    int SchemaVersion,
    EditorSettings Editor,
    LlmSettings Llm,
    IReadOnlyDictionary<string, string> Keybindings
)
{
    /// <summary>An immutable, empty keybindings dictionary.</summary>
    public static readonly IReadOnlyDictionary<string, string> EmptyKeybindings =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

    /// <summary>
    /// Default settings snapshot, corresponding to schema v1.
    /// Used when no settings file exists or when fallback is required.
    /// </summary>
    public static readonly SettingsModel Defaults = new(
        SchemaVersion: 1,
        Editor: EditorSettings.Default,
        Llm: LlmSettings.Default,
        Keybindings: EmptyKeybindings
    );

    /// <summary>
    /// Returns a defensive, immutable copy of the given keybindings map. The
    /// returned <see cref="ReadOnlyDictionary{TKey,TValue}"/> owns a fresh copy
    /// of the source entries, so mutating the caller's original dictionary
    /// cannot affect the published snapshot, and the published dictionary cannot
    /// be mutated through a cast or retained reference.
    /// </summary>
    /// <param name="source">The candidate keybindings map (may be null).</param>
    /// <returns>A non-null, read-only copy of <paramref name="source"/>.</returns>
    internal static IReadOnlyDictionary<string, string> NormalizeKeybindings(
        IReadOnlyDictionary<string, string>? source)
    {
        if (source is null)
            return EmptyKeybindings;

        // Always take a defensive copy. A ReadOnlyDictionary may itself wrap an
        // externally-owned mutable dictionary, so identity is never trusted even
        // when the input already reports as read-only. The published snapshot
        // therefore never shares a backing store with the caller's input.
        return new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(source));
    }
}

/// <summary>
/// Editor and terminal display preferences.
/// </summary>
public sealed record EditorSettings(
    [property: JsonPropertyName("codeFontFamily")]
    string CodeFontFamily,

    [property: JsonPropertyName("codeFontSize")]
    int CodeFontSize,

    [property: JsonPropertyName("proseFontFamily")]
    string ProseFontFamily,

    [property: JsonPropertyName("terminalFontFamily")]
    string TerminalFontFamily,

    [property: JsonPropertyName("terminalFontSize")]
    int TerminalFontSize,

    [property: JsonPropertyName("tabSize")]
    int TabSize,

    [property: JsonPropertyName("insertSpaces")]
    bool InsertSpaces,

    [property: JsonPropertyName("showWhitespace")]
    bool ShowWhitespace,

    [property: JsonPropertyName("showTabs")]
    bool ShowTabs,

    [property: JsonPropertyName("showSpaces")]
    bool ShowSpaces
)
{
    /// <summary>
    /// Default editor settings matching the Phase 8 umbrella schema v1.
    /// </summary>
    public static readonly EditorSettings Default = new(
        CodeFontFamily: "Cascadia Code, Consolas, monospace",
        CodeFontSize: 14,
        ProseFontFamily: "Georgia, serif",
        TerminalFontFamily: "Cascadia Code, JetBrains Mono, DejaVu Sans Mono, monospace",
        TerminalFontSize: 14,
        TabSize: 4,
        InsertSpaces: true,
        ShowWhitespace: false,
        ShowTabs: false,
        ShowSpaces: false
    );
}

/// <summary>
/// LLM endpoint configuration persisted to settings.
/// API key values are stored separately via <c>ISecretStore</c> (Phase 8.1.2).
/// </summary>
public sealed record LlmSettings(
    [property: JsonPropertyName("baseUrl")]
    string BaseUrl,

    [property: JsonPropertyName("model")]
    string Model,

    [property: JsonPropertyName("apiKeySource")]
    string ApiKeySource
)
{
    /// <summary>
    /// Default LLM settings matching the Phase 8 umbrella schema v1.
    /// </summary>
    public static readonly LlmSettings Default = new(
        BaseUrl: "https://api.openai.com/v1",
        Model: "gpt-4o-mini",
        ApiKeySource: "secret-store"
    );
}
