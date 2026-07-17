using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;

namespace Zaide.Features.Settings.Domain;

/// <summary>
/// Deeply immutable root settings model for the Zaide application.
/// All nested types are also immutable records. Consumers create new instances
/// via <c>with</c> expressions and cannot mutate a published snapshot.
/// </summary>
/// <param name="SchemaVersion">Version of the settings schema (currently <c>3</c>).</param>
/// <param name="Editor">Editor/terminal display preferences.</param>
/// <param name="Llm">LLM endpoint configuration.</param>
/// <param name="Keybindings">
/// User keybinding overrides as a flat <c>commandId → neutralGesture</c> map.
/// An empty-string value explicitly unbinds the command; it is not treated as a
/// missing override. The dictionary is always exposed as a read-only wrapper.
/// </param>
/// <param name="Debug">Debug-related persisted preferences such as breakpoints.</param>
public sealed record SettingsModel(
    int SchemaVersion,
    EditorSettings Editor,
    LlmSettings Llm,
    IReadOnlyDictionary<string, string> Keybindings,
    DebugSettings Debug
)
{
    /// <summary>An immutable, empty keybindings dictionary.</summary>
    public static readonly IReadOnlyDictionary<string, string> EmptyKeybindings =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

    /// <summary>
    /// Default settings snapshot, corresponding to schema v3.
    /// Used when no settings file exists or when fallback is required.
    /// </summary>
    public static readonly SettingsModel Defaults = new(
        SchemaVersion: 3,
        Editor: EditorSettings.Default,
        Llm: LlmSettings.Default,
        Keybindings: EmptyKeybindings,
        Debug: DebugSettings.Default
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

    /// <summary>
    /// Returns a defensive, immutable copy of the given debug settings snapshot.
    /// Workspace-root keys and source paths are normalized to absolute full paths.
    /// </summary>
    internal static DebugSettings NormalizeDebug(DebugSettings? source)
    {
        if (source is null)
            return DebugSettings.Default;

        var breakpoints = source.BreakpointsByWorkspaceRoot;
        if (breakpoints is null || breakpoints.Count == 0)
            return DebugSettings.Default;

        var copy = new Dictionary<string, IReadOnlyList<PersistedBreakpoint>>(
            breakpoints.Count,
            StringComparer.Ordinal);

        foreach (var (workspaceRoot, breakpointList) in breakpoints)
        {
            if (string.IsNullOrWhiteSpace(workspaceRoot))
                continue;

            copy[NormalizeAbsolutePath(workspaceRoot)] =
                NormalizeBreakpointList(breakpointList);
        }

        return new DebugSettings(
            new ReadOnlyDictionary<string, IReadOnlyList<PersistedBreakpoint>>(copy));
    }

    private static IReadOnlyList<PersistedBreakpoint> NormalizeBreakpointList(
        IReadOnlyList<PersistedBreakpoint>? source)
    {
        if (source is null || source.Count == 0)
            return Array.Empty<PersistedBreakpoint>();

        return source
            .Where(bp => !string.IsNullOrWhiteSpace(bp.SourcePath) && bp.Line > 0)
            .Select(bp => bp with { SourcePath = NormalizeAbsolutePath(bp.SourcePath) })
            .ToArray();
    }

    private static string NormalizeAbsolutePath(string path) => Path.GetFullPath(path);
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
    bool ShowSpaces,

    [property: JsonPropertyName("formatOnSave")]
    bool FormatOnSave = false
)
{
    /// <summary>
    /// Default editor settings matching schema v2 (Format on Save default off).
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
        ShowSpaces: false,
        FormatOnSave: false
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
/// Debug-related persisted settings. Adapter verification state is session-only
/// and is not stored here.
/// </summary>
public sealed record DebugSettings(
    [property: JsonPropertyName("breakpointsByWorkspaceRoot")]
    IReadOnlyDictionary<string, IReadOnlyList<PersistedBreakpoint>> BreakpointsByWorkspaceRoot
)
{
    /// <summary>An immutable, empty breakpoints-by-workspace map.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<PersistedBreakpoint>>
        EmptyBreakpointsByWorkspaceRoot =
            new ReadOnlyDictionary<string, IReadOnlyList<PersistedBreakpoint>>(
                new Dictionary<string, IReadOnlyList<PersistedBreakpoint>>());

    /// <summary>Default debug settings for schema v3.</summary>
    public static readonly DebugSettings Default = new(EmptyBreakpointsByWorkspaceRoot);
}

/// <summary>
/// One persisted source breakpoint (user intent only).
/// </summary>
/// <param name="SourcePath">Absolute normalized on-disk source path.</param>
/// <param name="Line">One-based source line.</param>
/// <param name="Enabled">Whether the breakpoint is enabled.</param>
public sealed record PersistedBreakpoint(
    [property: JsonPropertyName("sourcePath")]
    string SourcePath,

    [property: JsonPropertyName("line")]
    int Line,

    [property: JsonPropertyName("enabled")]
    bool Enabled = true
);
