using System;
using System.Collections.Generic;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Typed identifier for one unconditionally excluded IDE context category.
/// </summary>
internal readonly struct AgentContextHardExclusionId : IEquatable<AgentContextHardExclusionId>
{
    private readonly string? _value;

    private AgentContextHardExclusionId(string value)
    {
        _value = value;
    }

    public string Value => _value ?? string.Empty;

    public static AgentContextHardExclusionId TerminalScrollback { get; } =
        FromValue("context-exclusion:terminal-scrollback");

    public static AgentContextHardExclusionId DebugVariableWatchTrees { get; } =
        FromValue("context-exclusion:debug-variable-watch-trees");

    public static AgentContextHardExclusionId EnvironmentProcessSecrets { get; } =
        FromValue("context-exclusion:environment-process-secrets");

    public static AgentContextHardExclusionId FullLspInternals { get; } =
        FromValue("context-exclusion:full-lsp-internals");

    public static AgentContextHardExclusionId BinaryFileContent { get; } =
        FromValue("context-exclusion:binary-file-content");

    public static AgentContextHardExclusionId RedactionPatternMatch { get; } =
        FromValue("context-exclusion:redaction-pattern-match");

    public static IReadOnlyList<AgentContextHardExclusionId> All { get; } =
        new[]
        {
            TerminalScrollback,
            DebugVariableWatchTrees,
            EnvironmentProcessSecrets,
            FullLspInternals,
            BinaryFileContent,
            RedactionPatternMatch,
        };

    public static AgentContextHardExclusionId FromValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Hard exclusion id value is required.", nameof(value));
        }

        if (!value.StartsWith("context-exclusion:", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Hard exclusion id values must use the context-exclusion: prefix.",
                nameof(value));
        }

        return new AgentContextHardExclusionId(value);
    }

    public bool Equals(AgentContextHardExclusionId other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is AgentContextHardExclusionId other && Equals(other);

    public override int GetHashCode() =>
        _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    public static bool operator ==(
        AgentContextHardExclusionId left,
        AgentContextHardExclusionId right) =>
        left.Equals(right);

    public static bool operator !=(
        AgentContextHardExclusionId left,
        AgentContextHardExclusionId right) =>
        !left.Equals(right);

    public override string ToString() => Value;
}
