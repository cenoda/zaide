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
/// <param name="Keybindings">Keybinding overrides (reserved for Phase 8.2).</param>
public sealed record SettingsModel(
    int SchemaVersion,
    EditorSettings Editor,
    LlmSettings Llm,
    KeybindingOverrides Keybindings
)
{
    /// <summary>
    /// Default settings snapshot, corresponding to schema v1.
    /// Used when no settings file exists or when fallback is required.
    /// </summary>
    public static readonly SettingsModel Defaults = new(
        SchemaVersion: 1,
        Editor: EditorSettings.Default,
        Llm: LlmSettings.Default,
        Keybindings: KeybindingOverrides.Empty
    );
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

/// <summary>
/// Keybinding overrides (reserved for Phase 8.2).
/// Currently an empty placeholder.
/// </summary>
public sealed record KeybindingOverrides
{
    /// <summary>Singleton empty instance.</summary>
    public static readonly KeybindingOverrides Empty = new();
}
