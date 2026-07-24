using System;
using System.Text;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Lowercase SHA-256 digest over exact content bytes.
/// </summary>
internal readonly struct AgentContentRevision : IEquatable<AgentContentRevision>
{
    private readonly string? _value;

    private AgentContentRevision(string value)
    {
        _value = value;
    }

    public string Value => _value ?? string.Empty;

    public static AgentContentRevision FromDigest(string digest)
    {
        if (string.IsNullOrWhiteSpace(digest))
        {
            throw new ArgumentException("Content revision digest is required.", nameof(digest));
        }

        if (digest.Length != 64)
        {
            throw new ArgumentException(
                "Content revision digest must be 64 lowercase hexadecimal characters.",
                nameof(digest));
        }

        foreach (var character in digest)
        {
            var isHex = character is >= '0' and <= '9' or >= 'a' and <= 'f';
            if (!isHex)
            {
                throw new ArgumentException(
                    "Content revision digest must use lowercase hexadecimal characters.",
                    nameof(digest));
            }
        }

        return new AgentContentRevision(digest);
    }

    public static AgentContentRevision FromUtf8Text(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return FromBytes(Encoding.UTF8.GetBytes(text));
    }

    public static AgentContentRevision FromBytes(ReadOnlySpan<byte> bytes)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return FromDigest(Convert.ToHexString(hash).ToLowerInvariant());
    }

    public static AgentContentRevision MissingFile => FromDigest(
        "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");

    public bool Equals(AgentContentRevision other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is AgentContentRevision other && Equals(other);

    public override int GetHashCode() =>
        _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    public static bool operator ==(AgentContentRevision left, AgentContentRevision right) =>
        left.Equals(right);

    public static bool operator !=(AgentContentRevision left, AgentContentRevision right) =>
        !left.Equals(right);

    public override string ToString() => Value;
}
