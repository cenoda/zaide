using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Zaide.Features.Agents.Domain.Transparency;

/// <summary>
/// Stable workspace-scoped storage partition key. Derived from a normalized
/// workspace root path so clones, moves, and unrelated workspaces remain isolated.
/// </summary>
internal readonly struct AgentDurableWorkspaceStorageKey : IEquatable<AgentDurableWorkspaceStorageKey>
{
    private readonly string? _value;

    private AgentDurableWorkspaceStorageKey(string value)
    {
        _value = value;
    }

    public string Value => _value ?? string.Empty;

    public static AgentDurableWorkspaceStorageKey FromWorkspaceRoot(string workspaceRoot)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new ArgumentException("Workspace root is required.", nameof(workspaceRoot));
        }

        var normalized = Path.GetFullPath(workspaceRoot.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        var hex = Convert.ToHexString(hash)[..16].ToLowerInvariant();
        return new AgentDurableWorkspaceStorageKey($"ws:{hex}");
    }

    public static AgentDurableWorkspaceStorageKey FromValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Workspace storage key value is required.", nameof(value));
        }

        if (!value.StartsWith("ws:", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Workspace storage key must start with 'ws:'.",
                nameof(value));
        }

        return new AgentDurableWorkspaceStorageKey(value);
    }

    public bool Equals(AgentDurableWorkspaceStorageKey other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is AgentDurableWorkspaceStorageKey other && Equals(other);

    public override int GetHashCode() =>
        _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    public static bool operator ==(
        AgentDurableWorkspaceStorageKey left,
        AgentDurableWorkspaceStorageKey right) => left.Equals(right);

    public static bool operator !=(
        AgentDurableWorkspaceStorageKey left,
        AgentDurableWorkspaceStorageKey right) => !left.Equals(right);

    public override string ToString() => Value;
}
