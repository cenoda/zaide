using System;
using System.Text;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Immutable lowercase SHA-256 fingerprint for one exact action request.
/// </summary>
internal readonly struct AgentActionRequestFingerprint : IEquatable<AgentActionRequestFingerprint>
{
    private readonly string? _value;

    private AgentActionRequestFingerprint(string value)
    {
        _value = value;
    }

    public string Value => _value ?? string.Empty;

    public static AgentActionRequestFingerprint FromDigest(string digest)
    {
        if (string.IsNullOrWhiteSpace(digest))
        {
            throw new ArgumentException("Request fingerprint digest is required.", nameof(digest));
        }

        if (digest.Length != 64)
        {
            throw new ArgumentException(
                "Request fingerprint digest must be 64 lowercase hexadecimal characters.",
                nameof(digest));
        }

        foreach (var character in digest)
        {
            var isHex = character is >= '0' and <= '9' or >= 'a' and <= 'f';
            if (!isHex)
            {
                throw new ArgumentException(
                    "Request fingerprint digest must use lowercase hexadecimal characters.",
                    nameof(digest));
            }
        }

        return new AgentActionRequestFingerprint(digest);
    }

    public static AgentActionRequestFingerprint FromCanonicalText(string canonicalText)
    {
        ArgumentNullException.ThrowIfNull(canonicalText);
        var bytes = Encoding.UTF8.GetBytes(canonicalText);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return FromDigest(Convert.ToHexString(hash).ToLowerInvariant());
    }

    public bool Equals(AgentActionRequestFingerprint other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is AgentActionRequestFingerprint other && Equals(other);

    public override int GetHashCode() =>
        _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    public static bool operator ==(
        AgentActionRequestFingerprint left,
        AgentActionRequestFingerprint right) => left.Equals(right);

    public static bool operator !=(
        AgentActionRequestFingerprint left,
        AgentActionRequestFingerprint right) => !left.Equals(right);

    public override string ToString() => Value;
}
