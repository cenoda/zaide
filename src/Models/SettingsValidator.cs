using System.Collections.Generic;

namespace Zaide.Models;

/// <summary>
/// Validates a <see cref="SettingsModel"/> snapshot and returns field-level
/// <see cref="SettingsValidationError"/> items for every violation.
/// </summary>
public static class SettingsValidator
{
    /// <summary>
    /// Validates the given settings snapshot.
    /// An empty result means the snapshot is valid.
    /// </summary>
    public static IReadOnlyList<SettingsValidationError> Validate(SettingsModel settings)
    {
        var errors = new List<SettingsValidationError>();

        // ── Schema version ────────────────────────────────────────────────
        // Floor remains 1; current production model is schema v3 (Debug breakpoints).
        if (settings.SchemaVersion < 1)
            errors.Add(new(nameof(SettingsModel.SchemaVersion),
                "Schema version must be at least 1."));

        // ── Editor ────────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(settings.Editor.CodeFontFamily))
            errors.Add(new("Editor.CodeFontFamily",
                "Code font family must not be empty."));

        if (settings.Editor.CodeFontSize <= 0)
            errors.Add(new("Editor.CodeFontSize",
                "Code font size must be positive."));

        if (string.IsNullOrWhiteSpace(settings.Editor.ProseFontFamily))
            errors.Add(new("Editor.ProseFontFamily",
                "Prose font family must not be empty."));

        if (string.IsNullOrWhiteSpace(settings.Editor.TerminalFontFamily))
            errors.Add(new("Editor.TerminalFontFamily",
                "Terminal font family must not be empty."));

        if (settings.Editor.TerminalFontSize <= 0)
            errors.Add(new("Editor.TerminalFontSize",
                "Terminal font size must be positive."));

        if (settings.Editor.TabSize <= 0)
            errors.Add(new("Editor.TabSize",
                "Tab size must be positive."));

        // ── LLM ───────────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(settings.Llm.BaseUrl))
            errors.Add(new("Llm.BaseUrl",
                "Base URL must not be empty."));

        if (string.IsNullOrWhiteSpace(settings.Llm.Model))
            errors.Add(new("Llm.Model",
                "Model must not be empty."));

        if (string.IsNullOrWhiteSpace(settings.Llm.ApiKeySource))
            errors.Add(new("Llm.ApiKeySource",
                "API key source must not be empty."));

        // ── Debug ─────────────────────────────────────────────────────────
        if (settings.Debug.BreakpointsByWorkspaceRoot is not null)
        {
            foreach (var (workspaceRoot, breakpoints) in settings.Debug.BreakpointsByWorkspaceRoot)
            {
                if (string.IsNullOrWhiteSpace(workspaceRoot))
                {
                    errors.Add(new("Debug.BreakpointsByWorkspaceRoot",
                        "Workspace root keys must not be empty."));
                    continue;
                }

                if (breakpoints is null)
                    continue;

                for (var i = 0; i < breakpoints.Count; i++)
                {
                    var breakpoint = breakpoints[i];
                    if (string.IsNullOrWhiteSpace(breakpoint.SourcePath))
                    {
                        errors.Add(new($"Debug.BreakpointsByWorkspaceRoot[{workspaceRoot}][{i}].SourcePath",
                            "Breakpoint source path must not be empty."));
                    }

                    if (breakpoint.Line < 1)
                    {
                        errors.Add(new($"Debug.BreakpointsByWorkspaceRoot[{workspaceRoot}][{i}].Line",
                            "Breakpoint line must be at least 1."));
                    }
                }
            }
        }

        return errors.AsReadOnly();
    }
}
