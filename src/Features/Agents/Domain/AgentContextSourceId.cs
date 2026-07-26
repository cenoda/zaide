using System;
using System.Collections.Generic;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Typed identifier for one IDE context source category.
/// </summary>
internal readonly struct AgentContextSourceId : IEquatable<AgentContextSourceId>
{
    private readonly string? _value;

    private AgentContextSourceId(string value)
    {
        _value = value;
    }

    public string Value => _value ?? string.Empty;

    public static AgentContextSourceId BuildTestFailure { get; } =
        FromValue("context-source:build-test-failure");

    public static AgentContextSourceId DebugException { get; } =
        FromValue("context-source:debug-exception");

    public static AgentContextSourceId ProjectContext { get; } =
        FromValue("context-source:project-context");

    public static AgentContextSourceId ActiveFile { get; } =
        FromValue("context-source:active-file");

    public static AgentContextSourceId LanguageDiagnostics { get; } =
        FromValue("context-source:language-diagnostics");

    public static AgentContextSourceId BuildDiagnostics { get; } =
        FromValue("context-source:build-diagnostics");

    public static AgentContextSourceId TestResultsSummary { get; } =
        FromValue("context-source:test-results-summary");

    public static AgentContextSourceId WorkflowState { get; } =
        FromValue("context-source:workflow-state");

    public static AgentContextSourceId OpenFiles { get; } =
        FromValue("context-source:open-files");

    public static AgentContextSourceId SourceControlSummary { get; } =
        FromValue("context-source:source-control-summary");

    public static AgentContextSourceId DebugSessionState { get; } =
        FromValue("context-source:debug-session-state");

    public static AgentContextSourceId EditorCaretSelection { get; } =
        FromValue("context-source:editor-caret-selection");

    public static IReadOnlyList<AgentContextSourceId> All { get; } =
        new[]
        {
            BuildTestFailure,
            DebugException,
            ProjectContext,
            ActiveFile,
            LanguageDiagnostics,
            BuildDiagnostics,
            TestResultsSummary,
            WorkflowState,
            OpenFiles,
            SourceControlSummary,
            DebugSessionState,
            EditorCaretSelection,
        };

    public static AgentContextSourceId FromValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Context source id value is required.", nameof(value));
        }

        if (!value.StartsWith("context-source:", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Context source id values must use the context-source: prefix.",
                nameof(value));
        }

        return new AgentContextSourceId(value);
    }

    public bool Equals(AgentContextSourceId other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is AgentContextSourceId other && Equals(other);

    public override int GetHashCode() =>
        _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    public static bool operator ==(AgentContextSourceId left, AgentContextSourceId right) =>
        left.Equals(right);

    public static bool operator !=(AgentContextSourceId left, AgentContextSourceId right) =>
        !left.Equals(right);

    public override string ToString() => Value;
}
